# SmartSupport: AI Support Assistant for Your Team's Documents

SmartSupport is a multi-tenant **RAG (Retrieval-Augmented Generation)** support assistant. Each organization
uploads its own documents, and its members ask questions in a chat. Answers are written by an LLM **only from
those documents**, with the sources shown under each answer.

## 🌐 Live Demo

**https://smart-support-rag-engine.vercel.app**

Sign in with Google, create an organization (or join one with an invite link), upload a PDF, and start asking.

## 💡 Features

### For everyone
- **Grounded answers:** the model answers only from your organization's documents and says so when the answer isn't there.
- **Source citations:** each answer lists the documents it used; click a source to see the exact passage.
- **Conversation memory:** follow-up questions ("and for monthly plans?") work, because the chat history is sent to the model and follow-ups are rewritten into standalone search queries.
- **Streaming responses:** answers appear word by word, formatted as Markdown (lists, tables, bold, code).
- **Replies in your language:** answers come back in the language of the question.
- **Chat history:** sidebar grouped by date (Today, Yesterday, Previous 7 days, ...), with rename and delete.
- **Feedback:** rate answers 👍/👎 and copy them with one click.
- **Mobile-ready:** full-screen chat on phones, slide-over sidebar, keyboard-aware layout.

### Organizations and roles
- **Google sign-in** with a designed landing page and onboarding.
- **Organizations:** create one (you become its **admin**) or join with an **invite link / code** (as a **member**).
- **Data isolation:** each organization's documents, vectors and answers are separate; users only see their own chats.
- **Admin tools:**
  - **Documents page:** drag-and-drop multi-file upload (PDF, TXT, Markdown) with progress, and delete (also removes the vectors).
  - **Invite people:** copyable invite link, regenerate the code, member list.
  - **Answer feedback:** satisfaction rate and the answers members marked unhelpful, with their question and sources, to show where the documents need improving.

## 🏗 Architecture

```
┌─────────────────────┐     HTTPS + Google ID token     ┌──────────────────────────────┐
│  Angular client     │ ──────────────────────────────▶ │  .NET 9 API (Railway, Docker) │
│  (Vercel)           │ ◀── streamed answer (JSON) ──── │                              │
└─────────────────────┘                                 └──────┬───────────┬───────────┘
                                                               │           │
                     PostgreSQL (Neon) ◀── users, orgs,        │           │ ──▶ Qdrant Cloud
                     chats, documents, feedback ───────────────┘           │     (vectors, filtered by org)
                                                                           │
                                     Gemini (embeddings) ◀─────────────────┼───▶ Groq (answers, gpt-oss-120b)
```

**How a question is answered:**
1. If it's a follow-up, the LLM rewrites it into a standalone question.
2. The question is embedded with **Gemini** and searched in **Qdrant**, filtered to the caller's organization.
3. The best passages, the recent chat history and the question are sent to the LLM on **Groq**, with instructions to answer only from that context.
4. The answer streams back to the browser and is saved with its sources.

### Backend layers (`backend/`)

| Project | Role |
|---|---|
| `SmartSupport.Api` | Thin controllers, Google JWT auth, CORS, rate limiting, central error handling |
| `SmartSupport.Application` | Business logic (BLL): use-case services, access rules, DTOs, interfaces |
| `SmartSupport.Infrastructure` | Data access (DAL): EF Core repositories, migrations, plus Qdrant, Gemini, Groq and PDF adapters |
| `SmartSupport.Domain` | Entities only, no dependencies |

Dependencies point inward, every external service sits behind an interface, and everything is wired with
dependency injection. More detail: [backend/README.md](backend/README.md).

## 🛠 Tech Stack

| Area | Technology |
|---|---|
| Frontend | Angular 21 (standalone components, signals), ngx-toastr, SweetAlert2, marked |
| Backend | .NET 9 / ASP.NET Core, EF Core 9, Semantic Kernel |
| Database | PostgreSQL on [Neon](https://neon.tech) |
| Vector store | [Qdrant Cloud](https://qdrant.tech) |
| Embeddings | Google Gemini `gemini-embedding-001` (768 dimensions) |
| LLM | Groq, `openai/gpt-oss-120b` |
| Documents | PdfPig for PDF text extraction |
| Auth | Google Identity Services (ID tokens validated by the API) |
| Hosting | Vercel (frontend), Railway with Docker (backend) |

## 🔒 Security

- Google ID tokens are validated on every request (signature, issuer, audience, expiry).
- Organization-scoped data access in both the database and vector search; admin-only actions are enforced server-side.
- CORS limited to the app's own origins; rate limits on asking, uploading and joining (against invite-code guessing).
- Safe error responses (no internal details); questions sent in the request body, not the URL.
- Upload checks: 10 MB limit, allowed types only, PDF signature check, sanitized file names.
- Answers rendered as sanitized Markdown; document text is treated as data, not instructions, in the prompt.
- Secrets live only in environment variables; the container runs as a non-root user.

## 🔧 Running Locally

### Prerequisites
- .NET 9 SDK, Node.js 20+ and npm
- A PostgreSQL database (e.g. a free Neon project), a Qdrant Cloud cluster, a Gemini API key, a Groq API key,
  and a Google OAuth **Web** client ID (with `http://localhost:4200` as an authorized JavaScript origin)

### Backend
Set the settings with `dotnet user-secrets` (or a git-ignored `appsettings.Development.json`), never in a committed file:

```bash
cd backend/SmartSupport.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:SupabaseConnection" "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require"
dotnet user-secrets set "Authentication:Google:ClientId" "<your-client-id>.apps.googleusercontent.com"
dotnet user-secrets set "Qdrant:Host" "https://<your-cluster>.cloud.qdrant.io"
dotnet user-secrets set "Qdrant:ApiKey" "<qdrant-key>"
dotnet user-secrets set "Gemini:ApiKey" "<gemini-key>"
dotnet user-secrets set "Groq:ApiKey" "<groq-key>"
dotnet run
```

Database migrations run automatically on startup. Or run the API in Docker (from `backend/`):

```bash
docker build -t smartsupport-api .
docker run -p 8080:8080 --env-file .env smartsupport-api   # .env uses Section__Key names, e.g. Gemini__ApiKey=...
```

### Frontend
Set `API_URL` and `googleClientId` in `client/SmartSupport-Client/src/environments/environment.ts`, then:

```bash
cd client/SmartSupport-Client
npm install
npm start          # http://localhost:4200
```

## 🚀 Deployment

- **Backend (Railway):** Root Directory `/backend`, built from `backend/Dockerfile`. Configure with environment
  variables: `ConnectionStrings__SupabaseConnection`, `Authentication__Google__ClientId`, `Qdrant__Host`,
  `Qdrant__ApiKey`, `Gemini__ApiKey`, `Groq__ApiKey` (optional: `Groq__Model`, `Cors__AllowedOrigins__0`).
  `/health` is available for health checks.
- **Frontend (Vercel):** project root `client/SmartSupport-Client`; `vercel.json` rewrites all routes to the Angular app.

## 🗺 Roadmap

- Admin-only documents (per-document visibility)
- More file types (Word, HTML) and OCR for scanned PDFs
- Promote and remove members; more than one organization per user
- Automated tests for the Application layer and the API
