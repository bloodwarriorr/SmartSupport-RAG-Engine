
# SmartSupport - AI-Driven RAG System
A high-performance Support System based on RAG (Retrieval-Augmented Generation) architecture. This system combines semantic vector search with a Large Language Model (LLM) to provide accurate, context-aware answers based on your own documents.

🏗 Project Structure
The project is organized as a Monorepo for streamlined development:

**Backend**: .NET 10 API, Orchestration, and Vector integration.

**Client**: Angular 19 modern UI.

🛠 Tech Stack
Backend: .NET 10 (C#)

Frontend: Angular 19 (Standalone Components, Signals, RxJS)

Vector Database: Qdrant (Running via Docker)

AI Engine: Ollama (Llama 3 Model)

Orchestration: Semantic Kernel

Document Processing: PdfPig (PDF text extraction)

🚀 Getting Started
1. Infrastructure Setup
Run Qdrant (Vector DB):

Bash
docker run -p 6333:6333 -p 6334:6334 -v "$(pwd)/qdrant_storage:/qdrant/storage" qdrant/qdrant
Install Ollama & Models:

Download Ollama.

Pull the required models:

Bash
ollama pull llama3            # For text generation
ollama pull nomic-embed-text  # For vector embeddings
2. Backend Setup
Navigate to the directory: cd Backend.

Update appsettings.json with your SQL Connection String and Ollama endpoint.

Run the API:

Bash
dotnet run
The API will be available at http://localhost:5000 (or your configured port) with Swagger at /swagger.

3. Client Setup
Navigate to the directory: cd Client.

Install dependencies:

Bash
npm install
Run the development server:

Bash
ng serve
Access the UI at http://localhost:4200.

💡 Key Features
Multi-Format Ingestion: Supports .txt and .pdf file uploads with automatic text extraction and vectorization.

Real-time Streaming: Uses IAsyncEnumerable to stream AI tokens directly to the Angular UI for a smooth "typing" effect.

Semantic Search: Context-aware retrieval that eliminates hallucinations by grounding the LLM in specific knowledge base facts.

Clean Architecture: Strict adherence to SOLID principles and Dependency Injection for easy component swapping.

🛠 Future Roadmap

Admin Dashboard: Comprehensive file management and vector store monitoring.

Persistent History: Storing chat sessions in SQL Server.
