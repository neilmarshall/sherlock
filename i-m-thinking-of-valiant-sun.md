# Chat Assistant for Sherlock Holmes App

## Context

Add a simple chat assistant so users can ask questions about the currently-drawn story (e.g., "summarize the plot", "who are the main characters?"). An Azure AI Foundry deployment provides the LLM, and Microsoft Semantic Kernel wraps the chat orchestration. The chat lives as a second tab in the existing right-side `MetadataPanel`, so no layout refactor is needed. Responses stream token-by-token for snappier UX on longer answers.

**Decisions made:**
- **Auth:** API key via user secrets (set on AppHost, injected to API as env vars).
- **Local fallback:** Fail fast at startup if credentials missing. *Note: this softens the "always runnable locally with emulators" rule — README must document the required user secrets.*
- **UI:** Info/Chat tabs inside `MetadataPanel`.
- **Streaming:** Yes — SK `GetStreamingChatMessageContentsAsync` + fetch `ReadableStream` on the frontend.

## Approach

### Backend — `src/SherlockHolmes.Api`

1. **NuGet packages** (add to `SherlockHolmes.Api.csproj`):
   - `Microsoft.SemanticKernel`
   - `Microsoft.SemanticKernel.Connectors.AzureOpenAI`
   (Azure AI Foundry exposes models through an Azure OpenAI-compatible endpoint, so this connector is the right fit for chat-completion deployments.)

2. **Extend `StoryService`** (`Services/StoryService.cs`, `Services/IStoryService.cs`):
   Add `Task<string?> GetStoryBodyAsync(string id)` — fetches `BlobName` from the `stories` table, then downloads the blob content. Mirrors the 404-handling pattern already used by `GetStoryMetadataAsync`.

3. **New `ChatService`** (`Services/ChatService.cs`, `Services/IChatService.cs`):
   - Ctor injects `Kernel` and `IStoryService`.
   - `IAsyncEnumerable<string> StreamReplyAsync(string storyId, IReadOnlyList<ChatTurn> history, string userMessage, CancellationToken ct)`:
     - Calls `storyService.GetStoryBodyAsync(storyId)` (404 → throw a typed "story not found" exception the endpoint maps to 404).
     - Builds a `ChatHistory` with a system prompt like: *"You are a literary assistant answering questions strictly about the following Sherlock Holmes story. If asked about anything else, politely decline. Story:\n\n{body}"*.
     - Appends `history` turns, then the new user message.
     - Resolves `IChatCompletionService` from the kernel, calls `GetStreamingChatMessageContentsAsync`, yields each chunk's `Content` (skipping null/empty).
   - Models: `record ChatTurn(string Role, string Content);` and `record ChatRequest(IReadOnlyList<ChatTurn> History, string Message);` in `Models/`.

4. **`Program.cs` registration**:
   ```csharp
   var ai = builder.Configuration.GetSection("AzureOpenAI");
   var endpoint   = ai["Endpoint"]       ?? throw new InvalidOperationException("AzureOpenAI:Endpoint missing");
   var apiKey     = ai["ApiKey"]         ?? throw new InvalidOperationException("AzureOpenAI:ApiKey missing");
   var deployment = ai["DeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName missing");

   builder.Services.AddKernel()
       .AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
   builder.Services.AddSingleton<IChatService, ChatService>();
   ```

5. **New endpoint** in `Program.cs`:
   ```csharp
   app.MapPost("/api/stories/{id}/chat", (string id, ChatRequest req, IChatService chat, CancellationToken ct) =>
   {
       async IAsyncEnumerable<string> Stream([EnumeratorCancellation] CancellationToken token)
       {
           await foreach (var chunk in chat.StreamReplyAsync(id, req.History, req.Message, token))
               yield return chunk;
       }
       return TypedResults.Stream(async (stream, token) =>
       {
           await using var writer = new StreamWriter(stream);
           await foreach (var chunk in Stream(token))
           {
               await writer.WriteAsync(chunk);
               await writer.FlushAsync(token);
           }
       }, contentType: "text/plain; charset=utf-8");
   });
   ```
   Plain-text chunked streaming (not SSE) — simplest to produce on the server and read with `ReadableStream` in the browser.

### AppHost — `src/SherlockHolmes.AppHost/AppHost.cs`

Pass Foundry config from AppHost user secrets into the API as env vars:
```csharp
var foundryEndpoint   = builder.AddParameter("foundry-endpoint",   secret: true);
var foundryApiKey     = builder.AddParameter("foundry-api-key",    secret: true);
var foundryDeployment = builder.AddParameter("foundry-deployment");

var api = builder.AddProject<Projects.SherlockHolmes_Api>("api")
    .WithReference(tables)
    .WithReference(blobs)
    .WithEnvironment("AzureOpenAI__Endpoint",       foundryEndpoint)
    .WithEnvironment("AzureOpenAI__ApiKey",         foundryApiKey)
    .WithEnvironment("AzureOpenAI__DeploymentName", foundryDeployment)
    // ...existing wiring
```

### Frontend — `src/frontend`

1. **Install shadcn Tabs**: `npx shadcn@latest add tabs` (adds `components/ui/tabs.tsx`).

2. **New types** (`src/types/chat.ts`):
   ```ts
   export type ChatRole = 'user' | 'assistant';
   export interface ChatMessage { role: ChatRole; content: string }
   ```

3. **Extend `src/lib/api.ts`** — add `streamChatReply(storyId, history, message, onToken, signal)`:
   ```ts
   const res = await fetch(`/api/stories/${storyId}/chat`, {
     method: 'POST',
     headers: { 'Content-Type': 'application/json' },
     body: JSON.stringify({ history, message }),
     signal,
   });
   if (!res.ok || !res.body) throw new Error(`Chat failed: ${res.status}`);
   const reader = res.body.getReader();
   const decoder = new TextDecoder();
   for (;;) {
     const { value, done } = await reader.read();
     if (done) break;
     onToken(decoder.decode(value, { stream: true }));
   }
   ```

4. **New component** `src/components/ChatTab.tsx`:
   - Props: `storyId: string`.
   - State: `messages: ChatMessage[]`, `input: string`, `streaming: boolean`, `error: string | null`.
   - On submit: push user message, create an empty assistant message, call `streamChatReply` and append each token to the last assistant message. Disable the input while `streaming`.
   - Scroll-to-bottom on new content. Use `ScrollArea` from `components/ui/scroll-area.tsx`. Style with existing Victorian tokens (`bg-secondary` for user bubbles, `bg-background` border for assistant).

5. **Modify `src/components/MetadataPanel.tsx`**:
   - Wrap body in `<Tabs defaultValue="info">` with `<TabsList>` containing "Info" and "Chat" triggers.
   - "Info" tab: existing metadata block (no logic change).
   - "Chat" tab: `<ChatTab storyId={story.id} />` — add `key={story.id}` to reset chat state when a new case is drawn.

### Critical files to modify

- `src/SherlockHolmes.Api/SherlockHolmes.Api.csproj` — add SK packages.
- `src/SherlockHolmes.Api/Program.cs` — kernel registration, chat endpoint.
- `src/SherlockHolmes.Api/Services/StoryService.cs` + `IStoryService.cs` — add `GetStoryBodyAsync`.
- `src/SherlockHolmes.Api/Services/ChatService.cs` + `IChatService.cs` — new.
- `src/SherlockHolmes.Api/Models/ChatTurn.cs`, `ChatRequest.cs` — new.
- `src/SherlockHolmes.AppHost/AppHost.cs` — parameters + env vars.
- `src/frontend/src/types/chat.ts` — new.
- `src/frontend/src/lib/api.ts` — add `streamChatReply`.
- `src/frontend/src/components/ChatTab.tsx` — new.
- `src/frontend/src/components/ui/tabs.tsx` — from shadcn.
- `src/frontend/src/components/MetadataPanel.tsx` — add tabs.
- `README.md` / `CLAUDE.md` — document required user secrets.

### Reuse notes

- Existing `StoryService` caching + 404 handling (`Services/StoryService.cs:21-42`) — mirror it for `GetStoryBodyAsync`.
- Existing Aspire pattern: declare resource in AppHost → inject into API via env var → validate at startup. Applied here for Foundry config.
- Existing `MetadataPanel` Sheet layout and sticky header — no changes.
- `cn()` from `lib/utils.ts` for conditional classes; lucide `Send` / `MessageSquare` icons.

## Verification

1. **Configure secrets on AppHost** (one-time):
   ```bash
   cd src/SherlockHolmes.AppHost
   dotnet user-secrets set "Parameters:foundry-endpoint"   "https://<your-foundry>.openai.azure.com/"
   dotnet user-secrets set "Parameters:foundry-api-key"    "<key>"
   dotnet user-secrets set "Parameters:foundry-deployment" "<deployment-name>"
   ```

2. **Fail-fast check**: with one secret unset, `dotnet run --project src/SherlockHolmes.AppHost` — API should crash at startup with a clear message naming the missing key. Re-set and confirm clean startup.

3. **Happy path**:
   - Run AppHost, open frontend via the Aspire dashboard URL.
   - Draw a case → open metadata panel → switch to Chat tab.
   - Ask *"Summarize the plot in 3 sentences."* — tokens stream in live.
   - Ask *"Who are the main characters?"* — answer is grounded in the current story.
   - Ask *"What's the capital of France?"* — the system prompt should make the model politely decline (confirms grounding works).

4. **Reset on re-draw**: Draw a new case with chat already populated — Chat tab should be empty (state reset by `key={story.id}`).

5. **Cancel / unmount**: Close the panel mid-stream — fetch `AbortController` cancels the request; no console errors.

6. **Build gates**: `dotnet build src/SherlockHolmes.sln` clean; `cd src/frontend && npm run lint && npm run build` clean.

## Out of scope (possible follow-ups)

- Persisting chat history across reloads.
- Turn-count / token-budget cap (fine for v1; story bodies fit comfortably in `gpt-4o-mini`'s 128k context).
- Rate limiting or abuse handling on the chat endpoint.
- Tool use / function calling (SK plugins) — not needed for summarization-style Q&A.
