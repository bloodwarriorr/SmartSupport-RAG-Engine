# SmartSupport AI API

A modern Fullstack infrastructure for AI-powered document intelligence, built with .NET 9. This project implements a RAG (Retrieval-Augmented Generation) pipeline using Semantic Kernel, OpenAI, and Qdrant Vector Database.

## Overview

The system allows users to ingest unstructured text documents, process them into meaningful semantic chunks, and store them for high-speed similarity search. It uses a dual-storage strategy: 
- **SQL Server**: For structured metadata and document tracking.
- **Qdrant**: For high-dimensional vector storage and semantic search capabilities.

## Architecture & Tech Stack

- **Framework**: .NET 9 Web API
- **AI Orchestration**: Microsoft Semantic Kernel
- **Vector Database**: Qdrant (Running via Docker)
- **Relational Database**: SQL Server with Entity Framework Core
- **AI Models**: OpenAI `text-embedding-3-small` for semantic representation
- **Design Patterns**: 
  - Repository/Service Pattern
  - Dependency Inversion (SOLID Principles)
  - Asynchronous Programming (TAP)

## Key Features

### 1. Document Ingestion Pipeline
- Automated text chunking using Semantic Kernel's `TextChunker`.
- Concurrent embedding generation via OpenAI API.
- Synchronized storage across SQL (Metadata) and Qdrant (Vectors).

### 2. Semantic Search
- Natural language query processing.
- Vector-based similarity search to find relevant context even without keyword matches.
- Score-based ranking of search results.

### 3. Clean Architecture
- Loose coupling through Interfaces.
- Global exception handling and specific AI/Database error management.

## Getting Started

### Prerequisites
- Docker Desktop (for Qdrant)
- .NET 9 SDK
- OpenAI API Key
- SQL Server

### Configuration
Update your `appsettings.json` with your credentials:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Your_SQL_Server_Connection_String"
  },
  "OpenAI": {
    "ApiKey": "Your_OpenAI_Key",
    "ModelId": "text-embedding-3-small"
  }
}
