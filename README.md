# SmartSupport - AI-Driven RAG System
A high-performance Support System based on RAG (Retrieval-Augmented Generation) architecture. This system combines semantic vector search with a Large Language Model (LLM) to provide accurate, context-aware answers grounded in your own documents.

The system is fully production-ready, cloud-native, and accessible online.

## 🌐 Live Demo
The project is live and accessible at the following links:

Frontend (Production): https://smart-support-rag-engine.vercel.app

## 🏗 Project Structure
The project is organized as a Monorepo for streamlined development:

Backend: .NET 9 API, Orchestration, and Vector integration (Hosted on Railway).

Client: Angular 19 modern UI (Hosted on Vercel).

## 🛠 Tech Stack
Backend: .NET 9 (C#)

Frontend: Angular 19 (Standalone Components, Signals, RxJS)

Database (Persistence): Neon.tech - Serverless PostgreSQL for session and metadata storage.

Vector Database: Qdrant Cloud - Managed high-performance vector search engine.

AI Engine: Hugging Face APIs (for Embeddings) & OpenAI / Ollama (for Generation).

Orchestration: Semantic Kernel - Managing the RAG pipeline and AI memory.

Document Processing: PdfPig - Robust PDF text extraction.

## 🚀 Cloud Infrastructure & Deployment
The project utilizes a distributed cloud architecture to ensure high availability and global access:

API Hosting: Railway - Running the backend container with automated CI/CD directly from GitHub.

Web Hosting: Vercel - High-speed hosting for the Angular 19 frontend with edge delivery.

Managed Database: Neon.tech - Scalable PostgreSQL instance with optimized connection pooling for .NET.

Vector Store: Qdrant Cloud - Dedicated cloud environment for storing and searching high-dimensional vectors.

## 💡 Key Features
Live Cloud Deployment: A fully functional, "live" system that bridges frontend, backend, and multiple cloud databases.

Multi-Format Ingestion: Supports .txt and .pdf file uploads with automatic text extraction and vectorization.

Real-time Streaming: Uses IAsyncEnumerable to stream AI tokens directly to the UI for a smooth "typing" effect.

Semantic Search: Context-aware retrieval that eliminates hallucinations by grounding the LLM in specific facts.

Clean Architecture: Strict adherence to SOLID principles and Dependency Injection for professional-grade maintainability.

## 🔧 Installation & Local Setup
1. Prerequisites
.NET 9 SDK

Node.js & Angular CLI

API Keys for Hugging Face and OpenAI (or local Ollama instance)

2. Configuration
Update backend/SmartSupport.Api/appsettings.json with your cloud credentials:

JSON
{
  "ConnectionStrings": {
    "SupabaseConnection": "Host=ep-your-neon-host.neon.tech;Database=neondb;..."
  },
  "Qdrant": {
    "Url": "https://your-qdrant-cloud-url",
    "ApiKey": "your-qdrant-api-key"
  }
}
3. Running Locally
Backend:

Bash
cd backend/SmartSupport.Api
dotnet run
Frontend:

Bash
cd client
npm install
ng serve
🛠 Future Roadmap
Admin Dashboard: Comprehensive file management and vector store monitoring through the UI.

Multi-User Authentication: Implementing secure login and organization-based document isolation.

Image & Diagram Support: Enhancing PDF processing to include multi-modal RAG capabilities.
