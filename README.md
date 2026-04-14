# SmartSupport - AI-Driven RAG System
A high-performance Support System based on RAG (Retrieval-Augmented Generation) architecture. This system combines semantic vector search with a local Large Language Model (LLM) to provide accurate, context-aware answers.

## Tech Stack
Backend: .NET 10 (C#)

Vector Database: Qdrant (Running via Docker)

AI Engine: Ollama (Llama 3 Model)

Orchestration: Semantic Kernel

Frontend: Angular 19 (Under development)

## System Architecture
The project follows Clean Code and SOLID principles, ensuring a strict separation of concerns:

Ingestion Service: Handles document processing, converts text into vector embeddings, and stores them in Qdrant.

Search Service: Specializes in semantic similarity search within the vector database.

Ollama Service: Manages communication with the local LLM, supporting both standard and streaming responses.

Chat Service (Orchestrator): The "brain" of the system. It fetches relevant data via the Search Service and feeds it into the AI to generate grounded responses.

## Setup Instructions
1. Run Vector Database (Qdrant)
The system uses Qdrant to store and retrieve knowledge. Run the following command to start the container:

Bash
docker run -p 6333:6333 -p 6334:6334 -v "$(pwd)/qdrant_storage:/qdrant/storage" qdrant/qdrant
Note: Port 6334 is used for high-speed gRPC communication.

2. Install Ollama & Models
Download and install Ollama.

Open your terminal and pull the required models:

Bash
## LLM for Chat & Generation
ollama pull llama3

## Model for Text Embeddings (Vectorization)
ollama pull nomic-embed-text
3. Backend Configuration
Ensure your appsettings.json includes the correct endpoints:

JSON
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ModelId": "llama3"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=SmartSupport;..."
  }
}
## Key Features Implemented
Real-time Streaming
The system supports asynchronous streaming of AI responses. Using IAsyncEnumerable<string>, the backend pushes tokens to the client as they are generated. This significantly improves UX by reducing Time To First Token (TTFT).

## Semantic Context-Aware Search
Unlike traditional keyword search, this system understands user intent. AI responses are strictly grounded in the retrieved context from the Knowledge Base, effectively eliminating Hallucinations.

## Service-Oriented DI
Fully decoupled architecture using Dependency Injection and Interfaces, making the system highly testable and easy to swap components (e.g., switching from Ollama to Azure OpenAI).

## Running the Project
Navigate to the Backend directory: cd SmartSupport.Api.

Build the project: dotnet build.

Run the application: dotnet run.

Access Swagger UI to test the ask-stream and search endpoints.
