# Employee Documents Viewer

A lightweight internal web application for securely browsing and opening employee documents stored in Azure Blob Storage.

The application presents a searchable, sortable grid backed by a **SQL-based document catalog index**, ensuring fast and responsive user interaction even with large document sets.

Documents are **indexed into SQL for querying** and **streamed from Blob Storage for retrieval**.

---

## Architecture Overview

The application consists of five logical layers:

Browser (DataTables UI)
│
▼
Razor Pages UI
│
▼
FastEndpoints API
│
▼
Repository (SQL-backed query engine)
│
▼
Document Catalog (Azure SQL)
│
▼
Azure Blob Storage (file content only)

---

## Key Architectural Principle

**Blob Storage is not used as a query engine.**

Instead:

* Azure SQL stores a **document catalog index**
* The UI queries SQL for fast paging/filtering/sorting
* Blob Storage is used **only when opening a document**

This eliminates expensive container scans and enables sub-second response times even with large datasets (e.g., 60k+ documents).

---

## UI Layer

* ASP.NET Core **Razor Pages**

* **DataTables** grid (server-side mode)

* Fully supports:
  
  * paging
  * sorting
  * searching

Each row links to a document served via the API.

---

## API Layer

* Built using **FastEndpoints**

* Enforces authorization policies

* Returns:
  
  * JSON (document metadata)
  * streamed file responses (PDF, images, etc.)

---

## Data Layer

### Document Catalog (Azure SQL)

Primary query source for the application.

Stores indexed metadata:

* BlobName
* EmployeeId
* DocumentType
* Department (via join)
* Updated date
* Active / terminated status

This enables efficient:

* filtering
* sorting
* paging
* searching

### Document Storage (Azure Blob Storage)

Stores the actual document files.

Access pattern:

* No listing or querying at runtime
* Files retrieved **only when requested**

---

## Document Indexing

A background indexing process synchronizes Blob Storage with the SQL catalog.

### Responsibilities

* Enumerates blobs in the container
* Parses blob names into structured metadata
* Computes a SHA-256 hash for stable indexing
* Upserts records into SQL
* Marks deleted blobs as inactive

### Key Design Detail

Because blob names can exceed SQL index size limits:

* A **SHA-256 hash (BlobNameHash)** is used for indexing

* Full blob name is still stored for correctness

* Matching uses:
  
  * CompanyKey
  * BlobNameHash
  * BlobName

---

## Key Features

* Fast server-side paging, sorting, and filtering
* SQL-backed document catalog index
* Secure document streaming (no direct blob exposure)
* Vertical-slice architecture (FastEndpoints)
* Swagger UI for API testing
* Minimal infrastructure requirements

---

## Technology Stack

| Technology         | Purpose                          |
| ------------------ | -------------------------------- |
| ASP.NET Core       | Web application framework        |
| Razor Pages        | UI framework                     |
| FastEndpoints      | API endpoint framework           |
| DataTables         | Interactive data grid            |
| Swagger / OpenAPI  | API testing and documentation    |
| Bootstrap          | UI styling                       |
| Azure SQL          | Document catalog + employee data |
| Azure Blob Storage | Document storage                 |

---

## Project Structure

EmployeeDocumentsViewer
│
├─ Features
│   └─ Documents
│       ├─ Indexing
│       │   ├─ IDocumentCatalogIndexer.cs
│       │   ├─ SqlDocumentCatalogIndexer.cs
│       │   └─ DocumentCatalogSyncBackgroundService.cs
│       │
│       ├─ Read
│       │   ├─ List
│       │   │   ├─ Endpoint.cs
│       │   │   ├─ Request.cs
│       │   │   └─ Response.cs
│       │   │
│       │   └─ GetByBlobName
│       │       ├─ Endpoint.cs
│       │       └─ Request.cs
│       │
│       ├─ SqlDocumentRepository.cs
│       └─ DocumentBlobNameParser.cs
│
├─ Pages
│   └─ Documents
│       ├─ Index.cshtml
│       └─ Index.cshtml.cs
│
├─ Security
│   └─ DevAuthHandler.cs
│
└─ Program.cs

---

## API Endpoints

### List Documents

POST /api/documents/list

Returns paginated document metadata from SQL.

---

### Open Document

GET /api/documents/read/getbyblobname/{companyKey}?blobName={blobName}

Streams the file directly from Azure Blob Storage.

---

## Running the Application

### Requirements

* .NET 10+
* Azure SQL database
* Azure Storage account

---

### Run Locally

```bash
dotnet restore
dotnet build
dotnet run
```

Application URL:

https://localhost:7043/documents

---

## Swagger UI

Available in development:

https://localhost:7043/swagger

---

## Development Authentication

Uses:

DevAuthHandler

Automatically injects:

employee_portal = true

Production uses Azure Entra ID.

---

## Database Schema

The document catalog table:

Common.EmployeeDocumentCatalog

Key columns:

* Id (clustered PK)
* CompanyKey
* BlobName
* BlobNameHash (SHA-256)
* EmployeeId
* DocumentTypeDisplay
* UpdatedUtc
* IsDeleted

Indexes enable fast lookup and filtering.

---

## Performance Characteristics

### Previous design (deprecated)

* Full blob container scan per request
* In-memory filtering and sorting
* Poor scalability

### Current design

* SQL-based paging/filtering/sorting
* Constant-time query performance
* Blob access only on demand

---

## Production Architecture Example

Users
│
▼
Internal Web App
│
▼
Azure App Service
│
├── Azure SQL Database (document catalog + employee data)
│
└── Azure Blob Storage (documents)

---

## Security Considerations

* Azure Entra authentication
* Private blob containers
* No direct blob URLs exposed
* Files streamed through API
* Swagger disabled in production
  
  

---

## License

Internal use only.


