# SmartSupport backend

ASP.NET Core 9 API for SmartSupport: a retrieval-augmented (RAG) support assistant where each
organization chats with its own documents.

## Projects and layers

```
SmartSupport.Api             HTTP: controllers, Google JWT auth, CORS, rate limits, error handling
   │ uses
SmartSupport.Application     BLL: use-case services, business rules, DTOs, and the interfaces below
   │ uses
SmartSupport.Domain          Entities (User, Organization, Membership, ChatSession, ChatMessage, ...)

SmartSupport.Infrastructure  DAL + adapters: EF Core/PostgreSQL repositories and migrations,
                             Qdrant vector store, Gemini embeddings, Groq chat model, PDF parsing.
                             Implements the Application interfaces.
```

Dependencies only point inward: Domain depends on nothing, and Application knows interfaces
(`IChatRepository`, `IVectorStore`, `IChatModel`, ...) but no EF Core, Qdrant or vendor SDKs.
The Api project wires everything together with dependency injection (`AddApplication`,
`AddInfrastructure`, `AddApi`).

| Concern | Where |
|---|---|
| Access rules (member / admin) | `Application/Organizations/OrganizationAccess.cs`, used by every service |
| Business errors → HTTP status | `Application/Common/Exceptions` → `Api/Infrastructure/ApiExceptionHandler.cs` |
| Database mappings | `Infrastructure/Persistence/Configurations` |
| Migrations (applied on startup) | `Infrastructure/Persistence/Migrations`, `DatabaseMigrator.cs` |
| RAG tuning (search size, memory, sources) | `Application/Chat/RagOptions.cs` (`Rag` config section) |

## Configuration

All settings come from environment variables in production (Railway). Double underscores
separate sections. The app refuses to start if a required value is missing.

| Variable | Required | Notes |
|---|---|---|
| `ConnectionStrings__SupabaseConnection` | yes | PostgreSQL (Neon), `Host=...;Password=...` format |
| `Authentication__Google__ClientId` | yes | Same client id as the web app |
| `Qdrant__Host`, `Qdrant__ApiKey` | yes | Cluster URL and key |
| `Gemini__ApiKey` | yes | Embeddings (`Gemini__Model` defaults to `gemini-embedding-001`) |
| `Groq__ApiKey` | yes | Answers (`Groq__Model` defaults to `openai/gpt-oss-120b`) |
| `Cors__AllowedOrigins__0`, `__1`, ... | no | Defaults to the Vercel app and `http://localhost:4200` |

For local runs, keep secrets out of git: use `dotnet user-secrets` or the git-ignored
`appsettings.Development.json`.

## Running

```bash
# from backend/
dotnet build SmartSupport.sln
dotnet run --project SmartSupport.Api

# container (what Railway builds; Root Directory = /backend)
docker build -t smartsupport-api .
```

New migration (from `backend/`):

```bash
dotnet ef migrations add <Name> -p SmartSupport.Infrastructure -s SmartSupport.Api -o Persistence/Migrations
```

## Security notes

- Google ID tokens are validated (signature, issuer, audience, expiry) on every request; all
  endpoints except `/health` require sign-in.
- Every query is scoped to the caller's organization (database and vector search); users can only
  read and change their own chats; admin-only actions are checked in the Application layer.
- CORS allows only the configured frontends. Rate limits protect asking, uploading and joining
  with invite codes (against code guessing).
- Unexpected errors return a generic message; details go only to the server log.
- Uploads: 10 MB limit, extension allow-list, PDF signature check, file names stripped of paths.
- The document context sent to the model is marked as reference material, not instructions.
