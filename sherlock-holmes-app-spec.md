# Sherlock Holmes Short Story Reader — Technical Specification

## 1. Overview

A hobby web application that serves randomly selected Sherlock Holmes short stories on demand. The app is read-only (no user accounts, no personalisation, no write operations from the frontend). The aesthetic is Victorian-inspired: dark colours, period-appropriate typography.

---

## 2. Functional Requirements

### 2.1 Core Behaviour

- The landing page presents a single prominent call-to-action button (e.g. *"Draw a Case"*).
- Pressing the button fetches and displays a randomly selected short story from the backend.
- Pressing the button again replaces the current story with a newly randomised one.
- No deduplication or read-history tracking is required.

### 2.2 Story Display

- Full story text is displayed in a single scrollable view (no pagination in v1).
- A toggleable side panel displays story metadata (see 2.3).
- The side panel toggle should be accessible and intuitive on both desktop and mobile.
- Layout is mobile-friendly and responsive.

### 2.3 Story Metadata (Side Panel)

Each story record should carry the following metadata fields, displayed in the side panel when toggled open:

| Field | Notes |
|---|---|
| Title | e.g. *The Adventure of the Speckled Band* |
| Collection | e.g. *The Adventures of Sherlock Holmes* |
| Year Published | Original publication year |
| Word Count | Derived at import time |

> Additional metadata fields (e.g. notable characters) can be added later without structural changes.

### 2.4 Scope

- **56 canonical short stories** only. The four novels are explicitly out of scope.
- Stories are pre-loaded into storage by a separate data migration process. The frontend has no ability to create, update, or delete stories.
- No user authentication, sessions, or personalisation in v1.

---

## 3. Non-Functional Requirements

- **Read-only frontend**: all mutation of data happens out-of-band (migration tooling / admin scripts).
- **No cold starts**: storage and backend must respond promptly without warm-up delays.
- **Cost-effective**: appropriate for a low-traffic hobby project.
- **Portable hosting**: deployable to Azure or a self-hosted Linux VM with minimal changes.
- **Local dev experience**: full stack runnable locally using .NET Aspire.

---

## 4. Tech Stack

### 4.1 Backend — ASP.NET Core Minimal API (.NET 9)

- Thin HTTP API exposing two endpoints (see Section 5).
- No heavy framework overhead; Minimal API is well-suited to a small, stable route surface.
- Runs as a single deployable unit (containerisable via a standard `Dockerfile`).

### 4.2 Data Storage — Azure Table Storage (+ Azurite locally)

**Rationale:**

- No cold start: Table Storage is a serverless, always-on key-value/tabular store with consistent low-latency reads.
- Cost: Extremely cheap at hobby scale — effectively free under normal usage.
- Story text at 20–40 KB fits comfortably within a single Table Storage entity (1 MB entity limit).
- Azurite (the Azure Storage emulator) is natively supported as a .NET Aspire resource, giving a seamless local dev experience with no extra tooling.
- No schema migrations to manage; new metadata fields can be added as additional columns without disruption.
- Avoids the operational overhead of a relational database or a dedicated search service.

**Schema:**

| Property | Type | Notes |
|---|---|---|
| `PartitionKey` | string | Fixed value e.g. `"story"` — all stories in one partition for simple random selection |
| `RowKey` | string | Slugified story title e.g. `"speckled-band"` |
| `Title` | string | Display title |
| `Collection` | string | Source collection name |
| `YearPublished` | int | e.g. `1892` |
| `WordCount` | int | Computed at import time |
| `Body` | string | Full story text (UTF-8) |

> If story text ever exceeds the 1 MB entity limit (unlikely at 20–40 KB), the `Body` field can be offloaded to Azure Blob Storage with a reference URI stored in the entity — this requires no API contract changes.

### 4.3 Frontend — React (Vite + TypeScript)

**Rationale:**

- Given familiarity with both React and Blazor, React is recommended here for the following reasons:
  - Rich ecosystem of Victorian/dark-theme compatible UI libraries (see 4.4).
  - Vite provides an extremely fast local dev experience.
  - Easier to find high-quality, well-maintained component libraries tailored to reading/typography UX.
  - Smaller bundle than a Blazor WASM app for a project of this scope.
- Blazor remains a perfectly valid alternative if .NET-only toolchain is preferred.

**Structure:**

- Single-page application, no client-side routing required in v1.
- Communicates with the backend API via `fetch` / a lightweight HTTP client.
- Hosted as static files (served by the ASP.NET Core backend in production, or Vite dev server locally).

### 4.4 UI Library — shadcn/ui + Tailwind CSS

- **shadcn/ui** provides unstyled, accessible components (buttons, sheets/drawers for the side panel, scroll areas) that are straightforward to theme.
- **Tailwind CSS** makes it practical to apply a consistent Victorian dark theme (deep navy/charcoal backgrounds, parchment/sepia text, serif body font).
- Suggested fonts:
  - Body text: `Lora` or `Playfair Display` (Google Fonts — serif, period-appropriate).
  - UI chrome: `IM Fell English` or `Cinzel` for headings/buttons.

### 4.5 Local Orchestration — .NET Aspire

- The Aspire AppHost project wires together:
  - The ASP.NET Core API project.
  - An **Azurite** resource (Azure Storage emulator) — available as a first-class Aspire resource via `AddAzureStorage().RunAsEmulator()`.
  - The React frontend (via Vite dev server, launched as a Node app resource).
- Developers run a single `dotnet run` in the AppHost to get the full stack locally.
- Connection strings are injected automatically by Aspire; no manual `.env` management.

---

## 5. API Contract

### `GET /api/stories/random`

Returns a single randomly selected story including full text and metadata.

**Response `200 OK`:**
```json
{
  "id": "speckled-band",
  "title": "The Adventure of the Speckled Band",
  "collection": "The Adventures of Sherlock Holmes",
  "yearPublished": 1892,
  "wordCount": 8420,
  "body": "..."
}
```

### `GET /api/stories/{id}/metadata`

Returns metadata only for a given story (no body text). Useful for pre-fetching panel content cheaply if needed in future.

**Response `200 OK`:**
```json
{
  "id": "speckled-band",
  "title": "The Adventure of the Speckled Band",
  "collection": "The Adventures of Sherlock Holmes",
  "yearPublished": 1892,
  "wordCount": 8420
}
```

---

## 6. Data Migration

- A separate .NET console application (or script) handles the one-time import of story text files into Azure Table Storage.
- The migration tool should:
  - Read story `.txt` files from a local directory.
  - Compute word count.
  - Accept metadata (title, collection, year) via a companion JSON manifest or CSV.
  - Write entities to Table Storage (targeting either Azurite locally or the live Azure account).
- The migration tool is not part of the hosted application and does not need to be deployed.

---

## 7. Deployment

### Azure (primary target)

| Component | Azure Service |
|---|---|
| Backend API + static frontend | Azure App Service (Linux) or Azure Container Apps |
| Storage | Azure Table Storage (standard, no reserved capacity needed) |

- No cold start concern: App Service on Basic tier or above stays warm; Table Storage has no cold start by nature.
- Container Apps is a good alternative if containerisation is preferred and scale-to-zero cold starts are mitigated by minimum replica count of 1.

### Self-hosted Linux VM

- Backend published as a self-contained .NET binary or Docker container.
- Static frontend files served by the ASP.NET Core backend (`UseStaticFiles`).
- Azure Table Storage is accessed remotely (no local emulator in production); connection string provided via environment variable or secret manager.
- Reverse proxy via **nginx** or **Caddy** for TLS termination.

---

## 8. Out of Scope (v1)

- User accounts, authentication, or personalisation.
- Read history or "avoid repeats" logic.
- Story pagination.
- Search or filtering.
- Novels (*A Study in Scarlet*, *The Sign of the Four*, *The Hound of the Baskervilles*, *The Valley of Fear*).
- Admin UI for story management.
- Ratings or annotations.
