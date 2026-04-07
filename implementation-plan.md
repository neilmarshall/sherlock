# Sherlock Holmes Short Story Reader — Implementation Plan

## Prerequisites

### .NET Aspire on .NET 10
.NET 10 has **deprecated the Aspire workload** in favour of a pure NuGet/SDK-based model. Before implementing, ensure Aspire templates work correctly:

1. **Do NOT install the Aspire workload** (`dotnet workload install aspire` is obsolete on .NET 10).
2. Instead, scaffold using the Aspire AppHost SDK approach. The AppHost `.csproj` needs:
   ```xml
   <Sdk Name="Aspire.AppHost.Sdk" Version="9.0.0" />
   ```
   immediately after the `<Project>` tag (this is the Aspire SDK, distinct from the NuGet packages).
3. Test with: `dotnet new aspire` or `dotnet new aspire-starter` — if these templates aren't available, install them:
   ```bash
   dotnet new install Aspire.ProjectTemplates
   ```
4. Verify a basic AppHost builds and runs before proceeding.

### Other prerequisites
- **Node.js 20+** (for Vite/React frontend)
- **Docker** (Azurite runs as a container via Aspire)

---

## Context

Building a greenfield web app that serves randomly selected Sherlock Holmes short stories with a Victorian dark theme. The 56 canonical short stories are already downloaded as `.txt` files in `data/stories/`. All application code goes under `src/`.

---

## Spec Critique & Design Decisions

### Azure Table Storage — proceed with caution on body size
The spec claims stories are 20–40KB. **Actual range is 24–78KB.** Azure Table Storage has a **64KB limit per string property**. Three stories exceed this (Naval Treaty at 77KB, Copper Beeches at 73KB, Priory School at 71KB).

**Mitigation:** Store story bodies in **Azure Blob Storage** instead of inline in Table Storage. The table entity stores metadata + a blob reference. The API reads metadata from Table Storage and body from Blob Storage. This adds one extra read per request but avoids the size limit entirely and is the approach the spec itself suggests as a future option. Azurite supports both tables and blobs, so local dev is unaffected.

### Everything else in the spec is sound
- **React over Blazor** — correct for a typography-focused read-only SPA (smaller bundle, better font/theme ecosystem)
- **shadcn/ui + Tailwind** — good fit; Sheet component handles the side panel with proper accessibility
- **Single partition for 56 stories** — no scaling concern whatsoever
- **Aspire orchestration** — good local dev experience, wires Azurite + API + Vite together
- **Metadata-only endpoint** — cheap to implement, unused in v1 but harmless
- **Story text rendering** — raw Gutenberg text uses `_word_` for italics; the frontend should convert these to `<em>` tags

---

## Project Structure

```
src/
├── SherlockHolmes.sln
├── SherlockHolmes.AppHost/          # Aspire orchestrator
│   ├── SherlockHolmes.AppHost.csproj
│   └── Program.cs
├── SherlockHolmes.ServiceDefaults/  # Shared Aspire config (OpenTelemetry, health checks)
│   ├── SherlockHolmes.ServiceDefaults.csproj
│   └── Extensions.cs
├── SherlockHolmes.Api/              # ASP.NET Core Minimal API
│   ├── SherlockHolmes.Api.csproj
│   ├── Program.cs
│   ├── Models/
│   │   ├── StoryEntity.cs           # Table Storage entity (metadata + blob ref)
│   │   ├── StoryResponse.cs         # Full response DTO
│   │   └── StoryMetadataResponse.cs # Metadata-only DTO
│   └── Services/
│       ├── IStoryService.cs
│       └── StoryService.cs          # Table + Blob access, random selection
├── SherlockHolmes.Migration/        # Console app to seed data
│   ├── SherlockHolmes.Migration.csproj
│   ├── Program.cs
│   ├── StoryParser.cs
│   └── StoryMetadata.cs
└── frontend/                        # React + Vite + TypeScript
    ├── package.json
    ├── vite.config.ts
    ├── index.html
    ├── components.json              # shadcn/ui config
    └── src/
        ├── main.tsx
        ├── App.tsx
        ├── index.css                # Tailwind + Victorian theme
        ├── lib/
        │   ├── utils.ts             # shadcn cn() helper
        │   └── api.ts               # fetch wrapper
        ├── types/
        │   └── story.ts
        └── components/
            ├── ui/                  # shadcn primitives (button, sheet, scroll-area, skeleton)
            ├── DrawCaseButton.tsx
            ├── StoryView.tsx
            ├── MetadataPanel.tsx
            └── LandingHero.tsx
```

---

## Phase 1: Solution & Aspire Scaffolding

Create the .sln and all five projects targeting `net10.0`. Use Aspire templates where possible, then customise.

### AppHost Program.cs

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var tables = storage.AddTables("tables");
var blobs = storage.AddBlobs("blobs");

var api = builder.AddProject<Projects.SherlockHolmes_Api>("api")
    .WithReference(tables)
    .WithReference(blobs)
    .WaitFor(tables);

builder.AddViteApp("frontend", "../frontend")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
```

### Key NuGet packages

| Project | Packages |
|---------|----------|
| AppHost | `Aspire.Hosting.AppHost`, `Aspire.Hosting.Azure.Storage`, `Aspire.Hosting.JavaScript` |
| Api | `Aspire.Azure.Data.Tables`, `Aspire.Azure.Storage.Blobs` |
| Migration | `Azure.Data.Tables`, `Azure.Storage.Blobs` |

> Use the latest stable versions. At time of writing: Aspire packages are at **13.2.1**, Azure SDK packages at **12.x**.

### AppHost .csproj notes

The AppHost needs `IsAspireHost=true` and the Aspire AppHost SDK reference for the `Projects.*` source generator to work:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Aspire.AppHost.Sdk" Version="9.0.0" />
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsAspireHost>true</IsAspireHost>
  </PropertyGroup>
  <!-- ... -->
</Project>
```

### API .csproj Aspire integration

The API uses Aspire client integrations which auto-read connection strings injected by the AppHost:

```csharp
builder.AddAzureTableServiceClient("tables");
builder.AddAzureBlobServiceClient("blobs");
```

### Verify

`dotnet run` from AppHost starts Azurite container and API without errors.

---

## Phase 2: Data Migration Tool

Parse the 56 `.txt` files from `data/stories/` and seed Azurite.

### Story file format

Each file has a consistent format:
```
Title: The Adventure of the Speckled Band
Collection: The Adventures of Sherlock Holmes
Author: Arthur Conan Doyle
Source: Project Gutenberg (Public Domain)

============================================================

[Full story text]
```

### StoryParser.cs

```csharp
public record ParsedStory(string Title, string Collection, string Body, int WordCount);

public static class StoryParser
{
    private const string Separator = "============================================================";

    public static ParsedStory Parse(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var separatorIndex = content.IndexOf(Separator, StringComparison.Ordinal);
        if (separatorIndex < 0)
            throw new InvalidOperationException($"No separator found in {filePath}");

        var header = content[..separatorIndex];
        var body = content[(separatorIndex + Separator.Length)..].Trim();

        var title = ExtractField(header, "Title:");
        var collection = ExtractField(header, "Collection:");
        var wordCount = body.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).Length;

        return new ParsedStory(title, collection, body, wordCount);
    }

    private static string ExtractField(string header, string fieldName)
    {
        foreach (var line in header.Split('\n'))
        {
            if (line.StartsWith(fieldName, StringComparison.OrdinalIgnoreCase))
                return line[fieldName.Length..].Trim();
        }
        throw new InvalidOperationException($"Field '{fieldName}' not found in header");
    }
}
```

### StoryMetadata.cs — slug generation & year lookup

```csharp
public static class StoryMetadata
{
    private static readonly Dictionary<string, int> CollectionYears = new(StringComparer.OrdinalIgnoreCase)
    {
        ["The Adventures of Sherlock Holmes"] = 1892,
        ["The Memoirs of Sherlock Holmes"] = 1894,
        ["The Return of Sherlock Holmes"] = 1905,
        ["His Last Bow"] = 1917,
        ["The Case-Book of Sherlock Holmes"] = 1927,
    };

    public static int GetYearPublished(string collection)
        => CollectionYears.TryGetValue(collection, out var year) ? year : 0;

    public static string GenerateSlug(string title)
    {
        var slug = title;
        slug = slug.Replace("The Adventure of ", "", StringComparison.OrdinalIgnoreCase);
        slug = slug.Replace("The ", "", StringComparison.OrdinalIgnoreCase);
        slug = slug.Replace("A ", "", StringComparison.OrdinalIgnoreCase);
        slug = slug.ToLowerInvariant();
        slug = Regex.Replace(slug, "[^a-z0-9]+", "-");
        slug = Regex.Replace(slug, "-{2,}", "-");
        slug = slug.Trim('-');
        return slug;
    }
}
```

### Migration Program.cs

```csharp
var connectionString = args.Length > 0 ? args[0] : "UseDevelopmentStorage=true";
var storiesPath = args.Length > 1 ? args[1] : Path.GetFullPath("../../data/stories");

var tableServiceClient = new TableServiceClient(connectionString);
var tableClient = tableServiceClient.GetTableClient("stories");
await tableClient.CreateIfNotExistsAsync();

var blobServiceClient = new BlobServiceClient(connectionString);
var blobContainerClient = blobServiceClient.GetBlobContainerClient("stories");
await blobContainerClient.CreateIfNotExistsAsync();

foreach (var file in Directory.GetFiles(storiesPath, "*.txt"))
{
    var story = StoryParser.Parse(file);
    var slug = StoryMetadata.GenerateSlug(story.Title);

    // Upload body to blob storage
    var blobClient = blobContainerClient.GetBlobClient(slug);
    using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(story.Body));
    await blobClient.UploadAsync(bodyStream, overwrite: true);

    // Write metadata to table storage
    var entity = new TableEntity("story", slug)
    {
        { "Title", story.Title },
        { "Collection", story.Collection },
        { "YearPublished", StoryMetadata.GetYearPublished(story.Collection) },
        { "WordCount", story.WordCount },
        { "BlobName", slug },
    };
    await tableClient.UpsertEntityAsync(entity);
}
```

Uses `UpsertEntityAsync` so it's idempotent — safe to re-run.

### Verify

Run migration against local Azurite, confirm 56 table entities and 56 blobs.

---

## Phase 3: Backend API

### Models

```csharp
// StoryEntity.cs — implements ITableEntity
public class StoryEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "story";
    public string RowKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Collection { get; set; } = "";
    public int YearPublished { get; set; }
    public int WordCount { get; set; }
    public string BlobName { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

// DTOs
public record StoryResponse(string Id, string Title, string Collection, int YearPublished, int WordCount, string Body);
public record StoryMetadataResponse(string Id, string Title, string Collection, int YearPublished, int WordCount);
```

### StoryService.cs — random selection strategy

Cache all RowKeys in memory on first call (56 small rows), then point-read a random one per request:

```csharp
public class StoryService : IStoryService
{
    private readonly TableClient _tableClient;
    private readonly BlobContainerClient _blobContainerClient;
    private static List<string>? _cachedRowKeys;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public StoryService(TableServiceClient tableServiceClient, BlobServiceClient blobServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient("stories");
        _blobContainerClient = blobServiceClient.GetBlobContainerClient("stories");
    }

    public async Task<StoryResponse?> GetRandomStoryAsync()
    {
        var keys = await GetRowKeysAsync();
        if (keys.Count == 0) return null;

        var randomKey = keys[Random.Shared.Next(keys.Count)];
        var entity = (await _tableClient.GetEntityAsync<StoryEntity>("story", randomKey)).Value;

        var blobClient = _blobContainerClient.GetBlobClient(entity.BlobName);
        var blobResponse = await blobClient.DownloadContentAsync();
        var body = blobResponse.Value.Content.ToString();

        return new StoryResponse(entity.RowKey, entity.Title, entity.Collection,
            entity.YearPublished, entity.WordCount, body);
    }

    public async Task<StoryMetadataResponse?> GetStoryMetadataAsync(string id)
    {
        try
        {
            var entity = (await _tableClient.GetEntityAsync<StoryEntity>(
                "story", id, select: ["RowKey", "Title", "Collection", "YearPublished", "WordCount"])).Value;
            return new StoryMetadataResponse(entity.RowKey, entity.Title, entity.Collection,
                entity.YearPublished, entity.WordCount);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task<List<string>> GetRowKeysAsync()
    {
        if (_cachedRowKeys is not null) return _cachedRowKeys;
        await _lock.WaitAsync();
        try
        {
            if (_cachedRowKeys is not null) return _cachedRowKeys;
            var keys = new List<string>();
            await foreach (var entity in _tableClient.QueryAsync<StoryEntity>(
                filter: "PartitionKey eq 'story'", select: ["RowKey"]))
            {
                keys.Add(entity.RowKey);
            }
            _cachedRowKeys = keys;
            return keys;
        }
        finally { _lock.Release(); }
    }
}
```

### API Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddAzureTableServiceClient("tables");
builder.AddAzureBlobServiceClient("blobs");
builder.Services.AddSingleton<IStoryService, StoryService>();

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapGet("/api/stories/random", async (IStoryService svc) =>
{
    var story = await svc.GetRandomStoryAsync();
    return story is null ? Results.StatusCode(503) : Results.Ok(story);
});

app.MapGet("/api/stories/{id}/metadata", async (string id, IStoryService svc) =>
{
    var metadata = await svc.GetStoryMetadataAsync(id);
    return metadata is null ? Results.NotFound() : Results.Ok(metadata);
});

app.Run();
```

### Verify

`curl` both endpoints against local Azurite data.

---

## Phase 4: Frontend Scaffolding

### Setup commands

```bash
cd src/frontend
npm create vite@latest . -- --template react-ts
npm install tailwindcss @tailwindcss/vite
npx shadcn@latest init    # choose "new-york" style, CSS variables
npx shadcn@latest add button sheet scroll-area skeleton separator
npm install @fontsource/lora @fontsource/playfair-display @fontsource/cinzel
```

### vite.config.ts

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: parseInt(process.env.PORT ?? '5173'),
    proxy: {
      '/api': {
        target: process.env.services__api__https__0 ?? 'https://localhost:5001',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
```

### TypeScript types

```typescript
// src/types/story.ts
export interface Story {
  id: string;
  title: string;
  collection: string;
  yearPublished: number;
  wordCount: number;
  body: string;
}

export type StoryMetadata = Omit<Story, 'body'>;
```

### API client

```typescript
// src/lib/api.ts
import type { Story } from '../types/story';

export async function fetchRandomStory(): Promise<Story> {
  const res = await fetch('/api/stories/random');
  if (!res.ok) throw new Error(`Failed to fetch story: ${res.status}`);
  return res.json();
}
```

### Verify

Frontend loads in browser via Aspire dashboard.

---

## Phase 5: Frontend Features & Victorian Theme

### Components

- **LandingHero** — Victorian splash shown before first story draw. Title, tagline, prominent "Draw a Case" button.
- **DrawCaseButton** — Calls `GET /api/stories/random`, updates App state. Repositions to header/sticky bar after first draw.
- **StoryView** — Renders story title (Playfair Display) + body text (Lora) in a `ScrollArea`. Preserves paragraph breaks. Converts `_word_` patterns to `<em>` for italics.
- **MetadataPanel** — shadcn `Sheet` sliding from right (desktop) / bottom (mobile). Shows Title, Collection, Year, Word Count.

### Victorian theme (Tailwind CSS v4 `@theme` in index.css)

```css
@import "tailwindcss";

@theme {
  --color-background: #1a1a2e;        /* deep navy */
  --color-foreground: #e8d5b7;        /* parchment/sepia */
  --color-card: #16213e;              /* slightly lighter navy */
  --color-card-foreground: #e8d5b7;
  --color-primary: #c4a35a;           /* gold accent */
  --color-primary-foreground: #1a1a2e;
  --color-muted: #2a2a4a;
  --color-muted-foreground: #a89b8c;
  --color-border: #3d3d5c;
  --color-accent: #8b4513;            /* saddlebrown */

  --font-serif: 'Lora', serif;
  --font-display: 'Playfair Display', serif;
  --font-heading: 'Cinzel', serif;
}
```

### App.tsx state

```typescript
function App() {
  const [story, setStory] = useState<Story | null>(null);
  const [loading, setLoading] = useState(false);
  const [panelOpen, setPanelOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleDrawCase = async () => {
    setLoading(true);
    setError(null);
    try {
      const s = await fetchRandomStory();
      setStory(s);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Something went wrong');
    } finally {
      setLoading(false);
    }
  };

  // Render: LandingHero (no story) | StoryView + MetadataPanel (story loaded)
}
```

### States

- **Empty** — LandingHero with "Draw a Case" button
- **Loading** — Skeleton placeholders
- **Error** — Message with retry prompt
- **Loaded** — Story view with metadata panel toggle

---

## Phase 6: Polish

- Fade-in CSS transition on new story load
- Responsive layout testing on mobile viewports
- Production static file serving: API serves built frontend via `UseStaticFiles` + SPA fallback to `index.html`
- Add comprehensive `.gitignore` (bin, obj, node_modules, .env, etc.)

---

## Verification Checklist

1. `dotnet run` from AppHost — Aspire dashboard shows all resources healthy
2. Run migration tool — 56 entities in table, 56 blobs in container
3. `curl https://localhost:{port}/api/stories/random` — returns full story JSON
4. `curl https://localhost:{port}/api/stories/speckled-band/metadata` — returns metadata
5. Frontend loads, "Draw a Case" fetches and displays a story
6. Side panel toggles, shows correct metadata
7. Responsive layout on mobile viewport
8. Largest story (Naval Treaty, ~78KB body) renders correctly
