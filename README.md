# AgentFrameworkRag

An **agentic RAG** chat app: upload documents, ask questions, get streamed answers with source citations. The model decides **when** to search via MAF `TextSearchProvider` and the `search_documents` tool.

| Layer | Stack |
|---|---|
| Orchestration | .NET Aspire |
| Agents | Microsoft Agent Framework (`Microsoft.Agents.AI` 1.20) |
| API | ASP.NET Core + SSE streaming |
| AI | OpenAI / Azure OpenAI |
| Vector store | InMemory or Qdrant |
| Frontend | React 19, Vite, TypeScript |

> **Component details:** [docs/FEATURES.md](docs/FEATURES.md) — MAF vs non-MAF per file.

---

## How it works (visual)

### Agentic chat — the main flow

```mermaid
sequenceDiagram
    participant User
    participant RS as RagService
    participant Agent as document-assistant
    participant TSP as TextSearchProvider
    participant Search as DocumentRetrievalService

    User->>RS: message + history
    RS->>Agent: run (no pre-fetch)
    alt needs document info
        Agent->>TSP: search_documents(query)
        TSP->>Search: vector search
        Search-->>TSP: chunks + scores
        TSP-->>User: SSE sources (mid-stream)
        TSP-->>Agent: tool result
    else greeting or unrelated
        Note over Agent: skips search_documents
    end
    Agent-->>User: streamed answer
```

### Classic RAG vs agentic RAG (what changed)

**Before — always retrieve first:**

```mermaid
sequenceDiagram
    participant User
    participant RS as RagService
    participant Search as Retrieval
    participant Agent as AIAgent

    User->>RS: message
    RS->>Search: RetrieveAsync (always)
    Search-->>RS: chunks
    alt no relevant chunks
        RS-->>User: static reply (no LLM)
    else
        RS->>Agent: run with injected context
        Agent-->>User: answer
    end
```

**Now — agent decides when to search:**

```mermaid
sequenceDiagram
    participant User
    participant RS as RagService
    participant Agent as AIAgent
    participant TSP as TextSearchProvider

    User->>RS: message
    RS->>Agent: run (no pre-fetch)
    opt agent calls search_documents
        Agent->>TSP: search tool
        TSP-->>User: SSE sources
        TSP-->>Agent: results
    end
    Agent-->>User: streamed answer
```

### Which agent runs?

```mermaid
flowchart TD
    Start([User sends message]) --> HasDocs{Documents indexed?}
    HasDocs -->|No| General[general-assistant<br/>no tools]
    HasDocs -->|Yes| Doc[document-assistant<br/>search_documents tool]
    General --> Stream[Stream answer]
    Doc --> Decide{Model needs docs?}
    Decide -->|Yes| Tool[search_documents]
    Decide -->|No| Stream
    Tool --> Sources[SSE source pills]
    Sources --> Stream
    Stream --> Done([Done])
```

### What the UI receives (SSE timeline)

```mermaid
sequenceDiagram
    participant API
    participant UI as React UI

    Note over API,UI: Path A — search happened
    API->>UI: event sources → citation pills
    API->>UI: data token
    API->>UI: data token
    API->>UI: data [DONE]

    Note over API,UI: Path B — no search (e.g. hello)
    API->>UI: data token
    API->>UI: data token
    API->>UI: data [DONE]
```

---

## Architecture

### System map

```mermaid
flowchart TB
    subgraph browser [Browser]
        UI[React UI]
    end

    subgraph aspire [Aspire]
        FE[Vite :5173]
        API[API]
        QD[(Qdrant)]
    end

    OAI[OpenAI / Azure]

    UI --> FE -->|/api| API
    API --> OAI
    API --> QD
```

### Code layers

```mermaid
flowchart LR
    subgraph ui [UI + API]
        React[React]
        EP[Endpoints]
    end

    subgraph maf [MAF]
        RS[RagService]
        AG[AIAgent]
        TSP[TextSearchProvider]
    end

    subgraph bridge [Bridge]
        DSA[DocumentSearchAdapter]
        SC[SourceCollector]
    end

    subgraph data [Data plane]
        IDX[Indexer]
        RET[Retrieval]
        VS[(Vectors)]
    end

    React --> EP --> RS --> AG --> TSP
    TSP --> DSA --> RET --> VS
    DSA --> SC --> React
    EP --> IDX --> VS
```

| Layer | MAF? |
|---|---|
| UI + Endpoints | No |
| Agent + TextSearchProvider | **Yes** |
| Adapter + SourceCollector | Bridge |
| Indexer + Retrieval + Vectors | No |

---

## Request flows

### Upload a document

```mermaid
sequenceDiagram
    participant User
    participant API
    participant Index as Indexer
    participant VS as VectorStore

    User->>API: POST /documents/upload
    API->>API: extract text + chunk
    API->>Index: embed chunks
    Index->>VS: store vectors
    API-->>User: fileName + chunk count
```

### Chat with search (full path)

```mermaid
sequenceDiagram
    participant UI as React
    participant RS as RagService
    participant Agent
    participant TSP as TextSearchProvider
    participant RET as Retrieval
    participant OAI as OpenAI

    UI->>RS: POST /chat/stream
    RS->>Agent: RunStreamingAsync
    Agent->>OAI: completion + tools
    OAI-->>Agent: call search_documents
    Agent->>TSP: tool invoke
    TSP->>RET: embed + vector search
    RET-->>TSP: top chunks
    TSP-->>UI: SSE sources
    TSP-->>Agent: tool result
    Agent->>OAI: completion with context
    loop tokens
        OAI-->>UI: streamed text
    end
```

### Chat without search

```mermaid
sequenceDiagram
    participant UI as React
    participant Agent
    participant OAI as OpenAI

    UI->>Agent: "Hi there"
    Agent->>OAI: completion
    Note over Agent,OAI: no tool call
    OAI-->>UI: streamed reply
```

### No documents indexed

```mermaid
sequenceDiagram
    participant UI as React
    participant RS as RagService
    participant Agent as general-assistant
    participant OAI as OpenAI

    UI->>RS: POST /chat/stream
    RS->>Agent: no tools
    Agent->>OAI: general knowledge
    OAI-->>UI: streamed reply
```

---

## Features

- **Agentic RAG** — model chooses when to call `search_documents`
- **Live citations** — source pills before answer text streams
- **Multi-turn** — last 10 messages sent with each request
- **Semantic chunking** — 800 chars, 150 overlap
- **Relevance filter** — cosine score ≥ 0.3
- **Token budget** — ~3,000 tokens of context
- **Bounded tools** — max 3 search roundtrips per request
- **Qdrant optional** — persistent vectors via Aspire

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) with npm
- An **OpenAI API key** (or Azure OpenAI credentials)

---

## First-time setup

```bash
# 1. API key
cd AgentFrameworkRag.Api
dotnet user-secrets set "Ai:OpenAI:ApiKey" "sk-your-key-here"
cd ..

# 2. Frontend deps
cd react-frontend && npm install && cd ..

# 3. Restore
dotnet restore
```

---

## Running the application

```bash
cd AgentFrameworkRag
dotnet run
```

| Service | URL |
|---|---|
| Aspire dashboard | https://localhost:17181 |
| React frontend | http://localhost:5173 |
| API Reference (Scalar) | http://localhost:5123/scalar/v1 |
| Health check | http://localhost:5123/health |

```bash
cd AgentFrameworkRag.Api && dotnet run    # API only
cd react-frontend && npm run dev          # frontend only
dotnet test                                # unit tests
```

---

## Testing the API

```bash
# Health
curl http://localhost:5123/health

# Stream chat
curl -N -X POST http://localhost:5123/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{"message": "Summarise the uploaded document", "history": []}'

# Upload
curl -X POST http://localhost:5123/api/documents/upload -F "file=@/path/to/doc.pdf"

# List / delete
curl http://localhost:5123/api/documents
curl -X DELETE http://localhost:5123/api/documents/doc.pdf
```

---

## Using the React UI

1. Open `http://localhost:5173`
2. Upload documents in the sidebar
3. Ask a question — source pills appear, then the streamed answer
4. Try a follow-up or say "hello" (no search)
5. Use **Stop** to cancel streaming

---

## Configuration

### Azure OpenAI

```json
{ "Ai": { "Provider": "AzureOpenAI" } }
```

```bash
dotnet user-secrets set "Ai:AzureOpenAI:ApiKey" "your-key"
dotnet user-secrets set "Ai:AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
```

### Qdrant (persistent vectors)

```json
{ "Ai": { "VectorStore": { "Provider": "Qdrant" } } }
```

### RAG tuning (`Ai:Rag`)

| Setting | Default | Description |
|---|---|---|
| `TopK` | 6 | Chunks passed to the model |
| `CandidateK` | 20 | Vector search over-fetch |
| `MinRelevanceScore` | 0.3 | Min cosine similarity |
| `MaxContextTokens` | 3000 | Context token budget |
| `HistoryWindow` | 10 | Turns sent to agent |
| `MaxToolIterations` | 3 | Max tool roundtrips |

---

## Security note

Uploaded documents are injected into the LLM context — treat as untrusted input. No authentication; local dev and demos only.

---

## Project structure

```
AgentFrameworkRag/
├── AgentFrameworkRag/                 Aspire AppHost
├── AgentFrameworkRag.Api/             API + MAF agents + RAG
├── AgentFrameworkRag.Api.Tests/       xUnit tests
├── react-frontend/                    React UI
├── docs/FEATURES.md                   Component guide
└── README.md
```

---

## Further reading

- [docs/FEATURES.md](docs/FEATURES.md) — per-component what/why, MAF learning checklist
- [CLAUDE.md](CLAUDE.md) — developer commands
