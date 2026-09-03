# AgentFrameworkRag

A production-quality RAG (Retrieval-Augmented Generation) chat application built with:

- **.NET Aspire** — orchestration, telemetry, service discovery
- **Microsoft Agent Framework** — agent-based chat via `Microsoft.Agents.AI` and `Microsoft.Extensions.AI` (OpenAI primary, Azure OpenAI optional)
- **ASP.NET Core** — minimal API with SSE streaming
- **React 19 + Vite + TypeScript** — dark-themed chat UI
- **Tailwind CSS v4** — styling

---

## Features

- **Multi-turn conversation memory** — full chat history passed to the LLM; follow-up questions work naturally
- **Semantic-aware chunking** — text splits at paragraph → sentence → word boundaries, preserving meaning
- **Source citations** — every response includes source pills showing which document (and page) answered the question
- **Document management** — upload, list, and delete documents; duplicate uploads are rejected (409)
- **Relevance filtering** — chunks below cosine score 0.3 are excluded from context
- **Context token budget** — top-k chunks are trimmed to fit within 3,000 tokens
- **Persistent vector store (optional)** — swap `InMemory` for Qdrant via a single config flag; Aspire provisions the container automatically

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) with npm
- An **OpenAI API key** (or Azure OpenAI credentials)

---

## First-time Setup

### 1. Set your OpenAI API key

```bash
cd AgentFrameworkRag.Api
dotnet user-secrets set "Ai:OpenAI:ApiKey" "sk-your-key-here"
cd ..
```

### 2. Install npm dependencies

```bash
cd react-frontend
npm install
cd ..
```

### 3. Restore .NET packages

```bash
dotnet restore
```

---

## Running the Application

Run everything via the Aspire AppHost — it starts the API and React dev server together:

```bash
cd AgentFrameworkRag
dotnet run
```

The Aspire dashboard opens automatically. Both services are listed with live logs.

| Service | URL |
|---|---|
| Aspire dashboard | https://localhost:17181 |
| React frontend | http://localhost:5173 |
| API Reference (Scalar) | http://localhost:5123/scalar/v1 |
| API health check | http://localhost:5123/health |

> The exact API port is dynamically assigned by Aspire. `5123` is the default when running the API standalone. Vite proxies `/api` to the correct URL automatically.

---

## Testing the API

### Health check

```bash
curl http://localhost:5123/health
```

### Non-streaming chat (with optional history)

```bash
curl -X POST http://localhost:5123/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What is RAG?", "history": []}'
# {"reply":"...", "sources":[{"documentName":"...","chunkIndex":0,"pageNumber":1,"excerpt":"..."}]}
```

### Streaming chat (SSE)

```bash
curl -N -X POST http://localhost:5123/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{"message": "Summarise the uploaded document", "history": []}'
# event: sources
# data: [{"documentName":"report.pdf","chunkIndex":0,"pageNumber":2,"excerpt":"..."}]
#
# data: The document covers...
# data: [DONE]
```

### List documents

```bash
curl http://localhost:5123/api/documents
# ["report.pdf","notes.txt"]
```

### Upload a document

```bash
curl -X POST http://localhost:5123/api/documents/upload \
  -F "file=@/path/to/document.pdf"
# 200 OK: {"fileName":"document.pdf","chunks":42}
# 409 Conflict if already indexed — delete first
```

### Delete a document

```bash
curl -X DELETE http://localhost:5123/api/documents/document.pdf
# 204 No Content
```

### API Reference (Scalar)

Open `http://localhost:5123/scalar/v1` to explore and test all endpoints interactively.

---

## Using the React UI

1. Open `http://localhost:5173`
2. Upload one or more documents using the sidebar panel (`.txt`, `.md`, `.csv`, `.pdf` supported)
3. Ask questions — responses stream in token-by-token with source citations below each answer
4. Continue the conversation naturally; the LLM has full context of prior messages
5. Click **Stop** during streaming to cancel
6. Delete documents from the sidebar; re-upload the same file after deletion

---

## Switching to Azure OpenAI

1. In `AgentFrameworkRag.Api/appsettings.json`, change the provider:

```json
{
  "Ai": {
    "Provider": "AzureOpenAI"
  }
}
```

2. Set your Azure credentials as user secrets:

```bash
cd AgentFrameworkRag.Api
dotnet user-secrets set "Ai:AzureOpenAI:ApiKey" "your-azure-key"
dotnet user-secrets set "Ai:AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
```

3. Optionally update deployment names in `appsettings.json`:

```json
{
  "Ai": {
    "AzureOpenAI": {
      "ChatDeployment": "your-gpt4o-deployment-name",
      "EmbeddingDeployment": "your-embedding-deployment-name"
    }
  }
}
```

---

## Persistent Vector Store (Qdrant)

By default the app uses an in-memory vector store — all indexed documents are lost on restart. To persist across restarts, switch to Qdrant:

1. In `AgentFrameworkRag.Api/appsettings.json`:

```json
{
  "Ai": {
    "VectorStore": {
      "Provider": "Qdrant"
    }
  }
}
```

2. Run via Aspire (`cd AgentFrameworkRag && dotnet run`) — it provisions the Qdrant container automatically.

On restart, the `DocumentRegistrySeeder` repopulates the in-memory document registry by scanning the Qdrant collection, so previously indexed documents are immediately available.

---

## Project Structure

```
AgentFrameworkRag/
├── AgentFrameworkRag/                    Aspire AppHost (orchestrator)
│   ├── AppHost.cs                        Registers API, React frontend, Qdrant
│   └── AgentFrameworkRag.csproj
│
├── AgentFrameworkRag.ServiceDefaults/    Shared Aspire defaults
│   ├── Extensions.cs                     AddServiceDefaults(), OpenTelemetry, health checks
│   └── AgentFrameworkRag.ServiceDefaults.csproj
│
├── AgentFrameworkRag.Api/                ASP.NET Core Web API
│   ├── Agents/
│   │   ├── RagAgentFactory.cs            Creates general and document agents
│   │   ├── DocumentRagContextProvider.cs Injects retrieved chunks as agent context
│   │   └── RetrievalState.cs             Per-request retrieval state
│   ├── Configuration/
│   │   └── AiOptions.cs                  Provider + VectorStore config model
│   ├── Endpoints/
│   │   ├── ChatEndpoints.cs              POST /api/chat, POST /api/chat/stream (SSE)
│   │   └── DocumentEndpoints.cs          GET/POST/DELETE /api/documents
│   ├── Models/
│   │   └── DocumentChunk.cs              Vector store record (embedding + metadata)
│   ├── Services/
│   │   ├── RagService.cs                 IRagService — agent orchestration
│   │   ├── DocumentRetrievalService.cs   Embedding search + relevance filtering
│   │   ├── DocumentIndexerService.cs     Chunking, embedding, and indexing
│   │   ├── QueryContextualizer.cs        Rewrites follow-up queries with history
│   │   ├── TextChunker.cs                Semantic-aware chunking (paragraph/sentence/word)
│   │   ├── TextExtractor.cs              ITextExtractor — plain text + PDF (PdfPig)
│   │   ├── DocumentRegistry.cs           Singleton chunk-ID registry for deletion + dedup
│   │   ├── DocumentRegistrySeeder.cs     Repopulates registry from Qdrant on startup
│   │   └── TokenEstimator.cs             ~4 chars/token heuristic for context budgeting
│   └── Program.cs                        App startup, DI, AI client + vector store registration
│
├── react-frontend/                       React + Vite + TypeScript
│   └── src/
│       ├── api/client.ts                 Typed API client (fetch + SSE, history, delete)
│       ├── hooks/useChat.ts              Streaming chat state — history slice, sources
│       ├── components/
│       │   ├── chat/                     ChatWindow, MessageBubble (source pills), ChatInput
│       │   ├── documents/                DocumentUpload with delete (React Query)
│       │   └── layout/                   Sidebar, Header
│       └── types/index.ts                Message, SourceChunk interfaces
│
└── README.md
```

---

## Aspire Dashboard

The Aspire dashboard provides:
- **Logs** — real-time logs from API and frontend
- **Traces** — distributed traces showing HTTP requests and outbound calls to OpenAI
- **Metrics** — request counts, latency histograms
- **Resources** — start/stop/restart individual services

Access it at: `https://localhost:17181` (accept the self-signed certificate on first visit)
