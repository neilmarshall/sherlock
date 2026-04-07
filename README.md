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
- **API** (ASP.NET Core Minimal API) connected to Azurite
- **Frontend** (Vite dev server) proxying API requests to the backend

The Aspire dashboard URL will be printed to the console — open it to see all resources and their endpoints.

### 2. Seed the story data

The API won't return stories until the migration tool has been run. In a separate terminal:

```bash
dotnet run --project src/SherlockHolmes.Migration
```

This parses the 56 story text files from `data/stories/`, uploads each story body to Blob Storage, and writes metadata to Table Storage in the local Azurite emulator.

By default the migration tool connects to Azurite (`UseDevelopmentStorage=true`) and looks for stories at `data/stories/` relative to the project. You can override both via command-line args:

```bash
dotnet run --project src/SherlockHolmes.Migration -- "<connection-string>" "<stories-path>"
```

The tool is idempotent — safe to re-run at any time.

### 3. Use the app

Open the frontend URL shown in the Aspire dashboard (or the Vite dev server URL, typically `http://localhost:5173`). Click **"Draw a Case"** to fetch a random story.

## Project structure

| Project | Purpose |
|---------|---------|
| `src/SherlockHolmes.AppHost` | Aspire orchestrator — wires Azurite, API, and frontend |
| `src/SherlockHolmes.Api` | ASP.NET Core Minimal API (`/api/stories/random`, `/api/stories/{id}/metadata`) |
| `src/SherlockHolmes.Migration` | Console app to seed story data into Azure Storage |
| `src/frontend` | React + Vite + TypeScript + Tailwind CSS + shadcn/ui |
| `data/stories` | 56 canonical Sherlock Holmes short stories (plain text) |
