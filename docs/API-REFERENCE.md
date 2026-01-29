# Phlox API Reference

## Base URL
- Development: `https://localhost:7086`
- HTTP (Development): `http://localhost:5273`

## Authentication

All protected endpoints require a JWT Bearer token in the Authorization header:
```
Authorization: Bearer <token>
```

### Get Token
```http
POST /api/auth/login
Content-Type: application/json

{
    "email": "user@example.com",
    "password": "password123"
}
```

**Response:**
```json
{
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "expiresAt": "2024-01-25T12:00:00Z"
}
```

### Register
```http
POST /api/auth/register
Content-Type: application/json

{
    "email": "user@example.com",
    "username": "johndoe",
    "password": "password123",
    "name": "John Doe"
}
```

---

## Chat Endpoints

### Create Chat
Creates a new chat session for the authenticated user.

```http
POST /api/chat
Authorization: Bearer <token>
Content-Type: application/json

{
    "title": "My Chat Session"  // optional
}
```

**Response:** `201 Created`
```json
{
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "title": "My Chat Session",
    "createdAt": "2024-01-25T10:00:00Z",
    "updatedAt": null,
    "messages": []
}
```

### Get Chat
Retrieves a specific chat with all messages.

```http
GET /api/chat/{id}
Authorization: Bearer <token>
```

**Response:** `200 OK`
```json
{
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "title": "My Chat Session",
    "createdAt": "2024-01-25T10:00:00Z",
    "updatedAt": "2024-01-25T10:05:00Z",
    "messages": [
        {
            "id": "660e8400-e29b-41d4-a716-446655440001",
            "role": "user",
            "content": "What is RAG?",
            "createdAt": "2024-01-25T10:05:00Z"
        },
        {
            "id": "770e8400-e29b-41d4-a716-446655440002",
            "role": "assistant",
            "content": "RAG stands for Retrieval Augmented Generation...",
            "createdAt": "2024-01-25T10:05:05Z"
        }
    ]
}
```

### List Chats
Retrieves all chats for the authenticated user with pagination.

```http
GET /api/chat?page=1&pageSize=20
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| page | int | 1 | Page number |
| pageSize | int | 20 | Items per page |

**Response:** `200 OK`
```json
[
    {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "title": "My Chat Session",
        "createdAt": "2024-01-25T10:00:00Z",
        "updatedAt": "2024-01-25T10:05:00Z",
        "messages": [...]
    }
]
```

### Delete Chat
Deletes a chat and all its messages.

```http
DELETE /api/chat/{id}
Authorization: Bearer <token>
```

**Response:** `204 No Content`

### Send Message (RAG Query)
Sends a message to a chat and receives a streamed AI response.

```http
POST /api/chat/{id}/messages
Authorization: Bearer <token>
Content-Type: application/json

{
    "content": "What are the key features of our product?"
}
```

**Response:** `200 OK` (Server-Sent Events stream)
```
Content-Type: text/event-stream
Cache-Control: no-cache
Connection: keep-alive

data: {"content":"Based on "}

data: {"content":"the documentation, "}

data: {"content":"the key features are..."}

data: [DONE]
```

**Error Response:**
```
data: {"error":"An error occurred while generating the response."}
```

---

## Document Endpoints

### Create Document
Uploads a new document to the knowledge base.

```http
POST /api/document
Content-Type: application/json

{
    "title": "Product Documentation",
    "content": "<p>This is the product documentation...</p>"
}
```

**Response:** `201 Created`
```json
{
    "id": "880e8400-e29b-41d4-a716-446655440000",
    "title": "Product Documentation",
    "content": "<p>This is the product documentation...</p>",
    "createdAt": "2024-01-25T10:00:00Z",
    "updatedAt": null,
    "paragraphs": [
        {
            "id": "990e8400-e29b-41d4-a716-446655440001",
            "index": 0,
            "content": "This is the product documentation..."
        }
    ]
}
```

### Get Document
Retrieves a specific document with its paragraphs.

```http
GET /api/document/{id}
```

**Response:** `200 OK`
```json
{
    "id": "880e8400-e29b-41d4-a716-446655440000",
    "title": "Product Documentation",
    "content": "<p>This is the product documentation...</p>",
    "createdAt": "2024-01-25T10:00:00Z",
    "updatedAt": null,
    "paragraphs": [
        {
            "id": "990e8400-e29b-41d4-a716-446655440001",
            "index": 0,
            "content": "This is the product documentation..."
        }
    ]
}
```

### List Documents
Retrieves all documents with pagination.

```http
GET /api/document?page=1&pageSize=10
```

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| page | int | 1 | Page number |
| pageSize | int | 10 | Items per page |

**Response:** `200 OK`
```json
[
    {
        "id": "880e8400-e29b-41d4-a716-446655440000",
        "title": "Product Documentation",
        "content": "...",
        "createdAt": "2024-01-25T10:00:00Z",
        "updatedAt": null,
        "paragraphs": [...]
    }
]
```

### Delete Document
Deletes a document from both the database and vector store.

```http
DELETE /api/document/{id}
```

**Response:** `204 No Content`

---

## User Endpoints

### Get Current User
Retrieves the authenticated user's profile.

```http
GET /api/user/me
Authorization: Bearer <token>
```

**Response:** `200 OK`
```json
{
    "id": "aa0e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "username": "johndoe",
    "name": "John Doe",
    "createdAt": "2024-01-20T10:00:00Z",
    "lastLoginAt": "2024-01-25T10:00:00Z"
}
```

---

## Error Responses

### 400 Bad Request
```json
{
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
    "title": "Bad Request",
    "status": 400,
    "errors": {
        "Content": ["The Content field is required."]
    }
}
```

### 401 Unauthorized
```json
{
    "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
    "title": "Unauthorized",
    "status": 401
}
```

### 404 Not Found
```json
{
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
    "title": "Not Found",
    "status": 404
}
```

---

## Rate Limits

Currently no rate limiting is implemented. Consider implementing rate limiting for production use.

---

## OpenAPI Documentation

When running in development mode, OpenAPI documentation is available at:
- OpenAPI JSON: `https://localhost:7086/openapi/v1.json`
- Scalar UI: `https://localhost:7086/scalar`
