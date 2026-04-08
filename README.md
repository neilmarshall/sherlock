# Sherlock Holmes Short Story Reader

A Victorian-themed web app that serves randomly selected Sherlock Holmes short stories. Built with ASP.NET Core, React, and Azure Table/Blob Storage.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (required for running locally using the Azurite storage emulator)

## Running locally

### 1. Start the full stack

Make sure Docker is running, then from the repo root:

```bash
dotnet run --project src/SherlockHolmes.AppHost
```

This starts:
- **Azurite** (Azure Storage emulator) in a Docker container
- **Migration** — automatically seeds the 56 story text files from `data/stories/` into Azurite (runs once at startup, idempotent)
- **API** (ASP.NET Core Minimal API) connected to Azurite — waits for migration to complete
- **Frontend** (Vite dev server) proxying API requests to the backend

The Aspire dashboard URL will be printed to the console — open it to see all resources and their endpoints.

### 2. Use the app

Open the frontend URL shown in the Aspire dashboard. Aspire assigns a dynamic port to the Vite dev server on each run, so check the dashboard for the current URL. Click **"Draw a Case"** to fetch a random story.

## Project structure

| Project | Purpose |
|---------|---------|
| `src/SherlockHolmes.AppHost` | Aspire orchestrator — wires Azurite, Migration, API, and frontend |
| `src/SherlockHolmes.Api` | ASP.NET Core Minimal API (`/api/stories/random`, `/api/stories/{id}/metadata`) |
| `src/SherlockHolmes.Migration` | Console app to seed story data into Azure Storage (runs automatically via Aspire, or standalone — see below) |
| `src/frontend` | React + Vite + TypeScript + Tailwind CSS + shadcn/ui |
| `data/stories` | 56 canonical Sherlock Holmes short stories (plain text) |

## Running the migration standalone

The migration runs automatically as part of Aspire startup. To run it standalone against a different storage account (e.g. production), pass connection strings as environment variables:

```bash
ConnectionStrings__tables="<connection-string>" \
ConnectionStrings__blobs="<connection-string>" \
dotnet run --project src/SherlockHolmes.Migration
```

The tool is idempotent — safe to re-run at any time.
