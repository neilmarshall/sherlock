# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

**Prerequisites:** .NET 10 SDK, Node.js 20+, Docker (for Azurite emulator).

```bash
# Start the full stack (Azurite + Migration + API + Vite frontend) via Aspire
# Migration seeds story data automatically at startup
dotnet run --project src/SherlockHolmes.AppHost

# Build .NET projects
dotnet build src/SherlockHolmes.sln

# Frontend lint
cd src/frontend && npm run lint

# Frontend build
cd src/frontend && npm run build
```

The Aspire dashboard URL is printed at startup — use it to find service endpoints. Aspire assigns a dynamic port to the Vite dev server on each run.

## Architecture

This is a .NET Aspire-orchestrated app with three components:

- **AppHost** (`src/SherlockHolmes.AppHost`): Aspire orchestrator. Wires up Azurite (Azure Storage emulator running in Docker), the API, and the Vite frontend. All local development uses Azurite — no real Azure resources needed.
- **API** (`src/SherlockHolmes.Api`): ASP.NET Core Minimal API. Two endpoints:
  - `GET /api/stories/random` — returns a random story (metadata from Table Storage + body from Blob Storage)
  - `GET /api/stories/{id}/metadata` — returns metadata only
  - `StoryService` caches row keys in a static list (process-lifetime cache with double-check locking)
- **Migration** (`src/SherlockHolmes.Migration`): Console app that parses 56 `.txt` files from `data/stories/`, uploads bodies to Blob Storage, and writes metadata to Table Storage. Runs automatically as part of Aspire startup (API waits for it to complete). Can also run standalone with `ConnectionStrings__tables` and `ConnectionStrings__blobs` env vars.
- **Frontend** (`src/frontend`): React 19 + Vite + TypeScript + Tailwind CSS v4 + shadcn/ui components. Victorian theme with Playfair Display / Lora / Cinzel fonts.

## Key Patterns

- Storage uses Azure Table Storage (metadata) + Blob Storage (story bodies), accessed via `TableServiceClient` and `BlobServiceClient` from the Azure SDK.
- Aspire service defaults provide OpenTelemetry, health checks, and service discovery. The API project has a `/health` endpoint.
- The frontend proxies API requests through Vite's dev server (configured via Aspire's `WithReference`).
- For production, `PublishWithContainerFiles` bundles the built frontend into the API's `wwwroot` and serves it via `UseFileServer()`.
