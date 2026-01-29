# RAG (Retrieval Augmented Generation) System Documentation

## Overview

The Phlox RAG system enables intelligent question-answering by combining document retrieval with LLM-powered response generation. When a user asks a question, the system searches for relevant documents in the knowledge base and uses them as context to generate accurate, grounded answers.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              User Question                                   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                            ChatController                                    │
│                     POST /api/chat/{id}/messages                            │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              RagService                                      │
│  ┌────────────────────────────────────────────────────────────────────────┐ │
│  │ 1. Rewrite Query    │ 2. Search Docs    │ 3. Generate Answer           │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
                    │                   │                    │
                    ▼                   ▼                    ▼
        ┌───────────────────┐ ┌─────────────────┐ ┌───────────────────┐
        │ ChatCompletion    │ │  VectorService  │ │ ChatCompletion    │
        │ Service (OpenAI)  │ │    (Qdrant)     │ │ Service (OpenAI)  │
        │ Query Rewriting   │ │ Document Search │ │ Answer Generation │
        └───────────────────┘ └─────────────────┘ └───────────────────┘
                                      │
                                      ▼
                            ┌─────────────────┐
                            │   PostgreSQL    │
                            │ (Full Documents)│
                            └─────────────────┘
```

## RAG Workflow

### Step 1: Query Rewriting
The user's natural language question is optimized for vector search using OpenAI.

**Example:**
- User question: "What are the best practices for error handling in C#?"
- Rewritten query: "C# error handling best practices try catch exceptions"

### Step 2: Document Search
The system searches Qdrant for the top 3 most relevant documents:
1. Generate embedding for the rewritten query
2. Search Qdrant for similar paragraph embeddings
3. Group results by document ID
4. Select top 3 unique documents by best similarity score
5. Fetch full document content from PostgreSQL

### Step 3: Answer Generation
The LLM generates an answer using:
- The original user question
- Full content of the top 3 relevant documents
- A system prompt that enforces using only the provided context

## Components

### Services

#### IRagService / RagService
**Location:** `Services/RagService.cs`

Orchestrates the RAG workflow.

```csharp
public interface IRagService
{
    IAsyncEnumerable<string> GenerateAnswerAsync(
        string question,
        CancellationToken cancellationToken = default);
}
```

**Configuration:**
- `MaxSearchResults`: Number of documents to retrieve (default: 3)

#### IChatCompletionService / OpenAiChatCompletionService
**Location:** `Services/OpenAiChatCompletionService.cs`

Handles all OpenAI interactions.

```csharp
public interface IChatCompletionService
{
    // Stream chat completion with message history
    IAsyncEnumerable<string> StreamCompletionAsync(
        IEnumerable<MessageEntity> messages,
        string systemPrompt,
        CancellationToken cancellationToken = default);

    // Rewrite user query for better vector search
    Task<string> RewriteQueryForSearchAsync(
        string userQuery,
        CancellationToken cancellationToken = default);

    // Generate answer with document context
    IAsyncEnumerable<string> StreamAnswerWithContextAsync(
        string question,
        string documentContext,
        CancellationToken cancellationToken = default);
}
```

#### IVectorService / VectorService
**Location:** `Services/VectorService.cs`

Manages document storage and retrieval in Qdrant.

```csharp
public interface IVectorService
{
    // Add document to vector store
    Task<List<ParagraphEntity>> AddDocumentAsync(
        DocumentEntity document,
        CancellationToken cancellationToken = default);

    // Search for paragraphs (legacy)
    Task<List<SearchResult>> SearchAsync(
        string phrase,
        int limit = 3,
        CancellationToken cancellationToken = default);

    // Search for top N unique documents
    Task<List<DocumentSearchResult>> SearchDocumentsAsync(
        string phrase,
        int documentLimit = 3,
        CancellationToken cancellationToken = default);

    // Delete document from vector store
    Task DeleteDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
```

### DTOs

#### DocumentSearchResult
```csharp
public class DocumentSearchResult
{
    public Guid DocumentId { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }      // Full document content
    public float BestScore { get; set; }              // Highest similarity score
    public List<string> RelevantParagraphs { get; set; } = [];
}
```

## API Endpoints

### Chat Endpoints

#### Create Chat
```http
POST /api/chat
Authorization: Bearer {token}
Content-Type: application/json

{
    "title": "My Chat Session"
}
```

#### Send Message (RAG Query)
```http
POST /api/chat/{chatId}/messages
Authorization: Bearer {token}
Content-Type: application/json

{
    "content": "What are the key features of our product?"
}
```

**Response:** Server-Sent Events (SSE) stream
```
data: {"content": "Based on "}
data: {"content": "the documentation, "}
data: {"content": "the key features are..."}
data: [DONE]
```

#### Get Chat with Messages
```http
GET /api/chat/{chatId}
Authorization: Bearer {token}
```

### Document Endpoints

#### Upload Document
```http
POST /api/document
Content-Type: application/json

{
    "title": "Product Documentation",
    "content": "<html>...</html>"
}
```

#### Get Document
```http
GET /api/document/{documentId}
```

#### Delete Document
```http
DELETE /api/document/{documentId}
```

## Configuration

### appsettings.json

```json
{
  "OpenAi": {
    "ApiKey": "sk-...",
    "Model": "text-embedding-3-small",
    "ChatModel": "gpt-4o-mini",
    "Dimensions": 1536
  },
  "Qdrant": {
    "Host": "localhost",
    "Port": 6334,
    "CollectionName": "documents",
    "VectorSize": 1536,
    "ApiKey": null,
    "UseTls": false
  }
}
```

### Configuration Classes

#### OpenAiOptions
```csharp
public class OpenAiOptions
{
    public required string ApiKey { get; set; }
    public string Model { get; set; } = "text-embedding-3-small";
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public int Dimensions { get; set; } = 1536;
}
```

#### QdrantOptions
```csharp
public class QdrantOptions
{
    public required string Host { get; set; }
    public int Port { get; set; } = 6334;
    public string CollectionName { get; set; } = "documents";
    public int VectorSize { get; set; } = 768;
    public string? ApiKey { get; set; }
    public bool UseTls { get; set; } = false;
}
```

## System Prompts

### Query Rewriting Prompt
```
You are a search query optimizer. Your task is to rewrite the user's question
into an optimized search query for a vector database.

Rules:
- Extract key concepts and terms from the question
- Remove filler words and focus on semantic content
- Keep the query concise but comprehensive
- Output ONLY the rewritten query, nothing else
```

### RAG Answer Generation Prompt
```
You are a knowledgeable AI assistant. Your task is to answer the user's question
using ONLY the information provided in the document context below.

CRITICAL RULES:
- You MUST answer the question using ONLY the information from the provided document context
- NEVER say "I don't know", "I'm not sure", "The document doesn't contain", or similar phrases
- If the exact answer is not in the context, use the available information to provide
  the most relevant and helpful response possible
- Synthesize and connect information from different parts of the context to form
  a comprehensive answer
- Be confident and direct in your responses
- If you need to make reasonable inferences based on the context, do so without disclaimers
- Cite or reference specific parts of the documents when it adds value
- Maintain a helpful and informative tone throughout

Remember: The user expects you to be knowledgeable about the provided documents.
Always provide value from the context you have.
```

## Document Processing Pipeline

When a document is uploaded:

1. **HTML Cleaning**: Extract text and image descriptions
2. **Text Slicing**: Split into semantic paragraphs
3. **Embedding Generation**: Create vector embeddings for each paragraph
4. **Vector Storage**: Store in Qdrant with metadata
5. **Database Storage**: Store full document in PostgreSQL

```
Document Upload
      │
      ▼
┌─────────────┐
│ HTML Cleaner│ → Extract text, describe images
└─────────────┘
      │
      ▼
┌─────────────┐
│ Doc Slicer  │ → Split into paragraphs
└─────────────┘
      │
      ▼
┌─────────────┐
│ Embeddings  │ → Generate vectors (OpenAI)
└─────────────┘
      │
      ▼
┌─────────────┬─────────────┐
│   Qdrant    │  PostgreSQL │
│ (vectors +  │ (full docs) │
│  metadata)  │             │
└─────────────┴─────────────┘
```

## Data Storage

### Qdrant Point Structure
```json
{
  "id": "paragraph-guid",
  "vector": [0.1, 0.2, ...],
  "payload": {
    "document_id": "doc-guid",
    "paragraph_id": "paragraph-guid",
    "title": "Document Title",
    "paragraph_index": 0,
    "paragraph_content": "Paragraph text..."
  }
}
```

### PostgreSQL Schema

#### Documents Table
| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| Title | string | Document title |
| Content | string | Full HTML/text content |
| CreatedAt | DateTime | Creation timestamp |
| UpdatedAt | DateTime? | Last update timestamp |

#### Paragraphs Table
| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| DocumentId | GUID | Foreign key to Documents |
| Index | int | Paragraph order |
| Content | string | Paragraph text |

#### Chats Table
| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| OwnerId | GUID | Foreign key to Users |
| Title | string? | Chat title |
| CreatedAt | DateTime | Creation timestamp |
| UpdatedAt | DateTime? | Last activity timestamp |

#### Messages Table
| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| ChatId | GUID | Foreign key to Chats |
| Role | string | "user" or "assistant" |
| Content | string | Message content |
| CreatedAt | DateTime | Creation timestamp |

## Error Handling

### No Documents Found
When no relevant documents match the query:
```
"There are no documents in the knowledge base that match your question.
Please upload relevant documents first or try rephrasing your question."
```

### Stream Cancellation
If the user cancels the request mid-stream, partial responses are saved to the database.

### API Errors
OpenAI and Qdrant errors are logged and a generic error message is returned via SSE:
```json
{"error": "An error occurred while generating the response."}
```

## Performance Considerations

1. **Search Limit**: System searches for 5x the document limit to ensure enough unique documents
2. **Batch Upserts**: Documents are indexed in batches of 100 paragraphs
3. **Streaming**: Responses are streamed via SSE for better UX
4. **Caching**: Consider implementing query/embedding caching for frequently asked questions

## Security

- All chat endpoints require JWT authentication
- Users can only access their own chats
- Document endpoints can be configured with authorization
- API keys are stored in configuration, not in code

## Testing

### Unit Tests Location
```
Phlox.API.Tests/
├── Services/
│   ├── RagServiceTests.cs
│   ├── ChatCompletionServiceTests.cs
│   └── ...
└── Controllers/
    ├── ChatControllerTests.cs
    └── ...
```

### Running Tests
```bash
cd server
dotnet test
```

## Dependencies

- **OpenAI .NET SDK**: Chat completions and embeddings
- **Qdrant.Client**: Vector database operations
- **Entity Framework Core**: PostgreSQL ORM
- **ASP.NET Core**: Web API framework
