# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Run everything (recommended)
```bash
cd AgentFrameworkRag && dotnet run
```
Starts both the API and React dev server via .NET Aspire. The Aspire dashboard opens at `https://localhost:17181`.

### Run the API alone
```bash
cd AgentFrameworkRag.Api && dotnet run
```
API at `http://localhost:5123`, Scalar UI at `http://localhost:5123/scalar/v1`.

### Run the frontend alone
```bash
cd react-frontend && npm run dev
```
Vite dev server at `http://localhost:5173`.

### Build
```bash
dotnet build                          # .NET solution
cd react-frontend && npm run build    # React (tsc + vite build)
```

### Lint (frontend)
```bash
cd react-frontend && npm run lint
```

### First-time setup
```bash
# API key (required)
cd AgentFrameworkRag.Api && dotnet user-secrets set "Ai:OpenAI:ApiKey" "sk-..."

# Restore packages
dotnet restore
cd react-frontend && npm install
```

## Architecture

### Overview
This is a RAG (Retrieval-Augmented Generation) chat app. Documents are uploaded, chunked, embedded via OpenAI, stored in a vector store, and retrieved as context for chat queries. Chat is powered by Microsoft Agent Framework agents.

### .NET Aspire orchestration (`AgentFrameworkRag/AppHost.cs`)
The Aspire AppHost is the single entry point for development. It registers:
- `agentframeworkrag-api` — the ASP.NET Core API project
- `agentframeworkrag-frontend` — runs `npm run dev` inside `react-frontend/`; injects `VITE_API_URL` so Vite's proxy targets the dynamically assigned API port

`AgentFrameworkRag.ServiceDefaults/Extensions.cs` wires up OpenTelemetry, health checks, and service discovery via `AddServiceDefaults()`.

### API (`AgentFrameworkRag.Api/`)
Minimal API project targeting .NET 10. Key wiring in `Program.cs`:
- `IChatClient` and `IEmbeddingGenerator<string, Embedding<float>>` registered from OpenAI or Azure OpenAI (controlled by `Ai:Provider` in appsettings)
- `VectorStore` via `CommunityToolkit.VectorData` — `InMemoryVectorStore` (default) or `QdrantVectorStore`
- `DocumentRegistry` singleton; `IRagService`, `RagAgentFactory`, and retrieval services registered as scoped
- File upload limit is 50 MB

**Endpoints:**
- `ChatEndpoints` — `POST /api/chat` (non-streaming) and `POST /api/chat/stream` (SSE streaming)
- `DocumentEndpoints` — `GET /api/documents`, `POST /api/documents/upload`, `DELETE /api/documents/{name}`

**RAG pipeline (`Services/` + `Agents/`):**
- `RagService` — orchestrates agents: uses a general-knowledge agent when no documents are indexed, otherwise creates a document agent per request
- `RagAgentFactory` — builds `AIAgent` instances via `Microsoft.Agents.AI`; document agent uses `DocumentRagContextProvider` to inject retrieved chunks
- `DocumentRagContextProvider` — `AIContextProvider` that supplies retrieved document context to the agent
- `DocumentRetrievalService` — embeds the query, searches top-k chunks, filters by relevance score
- `DocumentIndexerService` — chunks, embeds, and stores documents in the vector store
- `QueryContextualizer` — rewrites follow-up questions using conversation history for better retrieval
- `TextChunker` — splits text into 800-character chunks with 150-char overlap, breaking at word boundaries
- `TextExtractor` — `ITextExtractor` strategy interface with `PlainTextExtractor` (.txt/.md/.csv) and `PdfTextExtractor` (.pdf via PdfPig)
- `DocumentRegistry` — singleton `ConcurrentDictionary` tracking indexed document names and chunk IDs
- `DocumentChunk` — vector store model via `Microsoft.Extensions.VectorData`; embedding dimension is fixed at 1536 (matches `text-embedding-3-small`)

### React frontend (`react-frontend/src/`)
- `api/client.ts` — raw fetch functions: `chatStreamFetch` (async generator over SSE), `chatOnce`, `listDocuments`, `uploadDocument`
- `hooks/useChat.ts` — all streaming state: appends chunks to the assistant message in place, exposes `stopStreaming` via `AbortController`
- `components/chat/` — `ChatWindow`, `MessageBubble`, `ChatInput`
- `components/documents/DocumentUpload.tsx` — uses React Query for the upload mutation and document list query
- Vite proxies `/api` → `VITE_API_URL` (set by Aspire) or falls back to `http://localhost:5123`
- Path alias `@` maps to `src/`

### AI provider configuration
Provider is selected via `Ai:Provider` in `appsettings.json` (`"OpenAI"` or `"AzureOpenAI"`). Credentials are always set via `dotnet user-secrets` — never in appsettings. Model defaults: `gpt-4o` for chat, `text-embedding-3-small` for embeddings.

### Extending the vector store
`InMemoryVectorStore` is a development convenience — all indexed documents are lost on restart. To persist, set `Ai:VectorStore:Provider` to `"Qdrant"` in appsettings; Aspire provisions the container automatically. Update the `DocumentChunk` embedding dimension if switching embedding models.
