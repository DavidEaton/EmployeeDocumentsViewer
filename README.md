Employee Documents Viewer
=========================

A lightweight internal web application for securely browsing and opening employee documents from Azure blob storage.

The application presents a searchable, sortable grid of documents. Each row contains a hyperlink that opens the document (PDF) directly from the server.

This project demonstrates a simple architecture for serving document metadata from a database while streaming the document content through a secure API endpoint.

* * *

Architecture Overview
=====================

The application consists of three primary layers:

Browser (DataTables UI)  
        │  
        ▼  
Razor Pages UI  
        │  
        ▼  
FastEndpoints API  
        │  
        ▼  
Repository  
        │ │  
        ▼▼  
Document Storage (Azure Blob Storage) | Employe Data (Azure SQL Database)

### UI Layer

* ASP.NET Core **Razor Pages**

* **DataTables** grid for sorting, searching, and paging

* Each row contains a link that opens a document

### API Layer

* **FastEndpoints** is used for HTTP endpoints

* Endpoints enforce authorization policies

* Endpoints return either:
  
  * JSON (for the document list)
  
  * a streamed PDF file

### Data Layer

Reads metadata from **Azure SQL Database**


Document files themselves are stored in **Azure Blob Storage**

Key Features
============

* Server-side paging, sorting, and filtering

* FastEndpoints vertical-slice architecture

* Secure document streaming

* Swagger UI for API testing

* Simple Razor Pages UI

* Minimal infrastructure requirements

Technology Stack
================

| Technology        | Purpose                       |
| ----------------- | ----------------------------- |
| ASP.NET Core      | Web application framework     |
| Razor Pages       | UI framework                  |
| FastEndpoints     | API endpoint framework        |
| DataTables        | Interactive data grid         |
| Swagger / OpenAPI | API testing and documentation |
| Bootstrap         | Basic styling                 |
| Azure Storage     | Secure Document storage (cloud)|
| Azure SQL Database| Employee data                 |


* * *

Project Structure
=================

EmployeeDocumentsViewer  
│  
├─ Features  
│   └─ Documents  
│       └─ Read  
│           ├─ List  
│           │   ├─ Endpoint.cs  
│           │   ├─ Request.cs  
│           │   └─ Response.cs  
│           │  
│           └─ GetById  
│               ├─ Endpoint.cs  
│               └─ Request.cs  
│  
├─ Pages  
│   └─ Documents  
│       ├─ Index.cshtml  
│       └─ Index.cshtml.cs  
│  
├─ Security  
│   └─ DevAuthHandler.cs  
│  
├─ Common  
│  
└─ Program.cs

This structure follows a **vertical slice architecture**, where each feature contains its own endpoint, request contract, and response contract.

* * *

API Endpoints
=============

List Documents
--------------

POST /api/documents/read/list

Returns paginated document metadata used by the grid.

Example response:

```json
{  
  "draw": 1,  
  "recordsTotal": 4,  
  "recordsFiltered": 4,  
  "data": [  
    {  
      "documentId": 1,  
      "employee": "Alice Carter",  
      "department": "HR",  
      "documentType": "Policy",  
      "year": 2026  
    }  
  ]  
}
```

* * *

Open Document
-------------

`GET /api/documents/open/{id}`

Streams the PDF document associated with the specified ID.

Example:

`GET /api/documents/open/2`

Returns

`Content-Type: application/pdf`

* * *

Running the Application
=======================

Requirements
------------

* .NET 8+

* Node.js not required

* Docker optional

* * *

Run Locally
-----------

```bash
dotnet restore  
dotnet build  
dotnet run
```

The application will start at:

`http://localhost:5129`

Open:

`http://localhost:5129/documents`

* * *

Swagger UI
==========

Swagger UI is available in development mode.

`http://localhost:5129/swagger`

It allows testing the API endpoints directly.

* * *

Development Authentication
==========================

The project includes a development authentication handler:

`DevAuthHandler`

This automatically authenticates requests with the required claim:

`employee_portal = true`

This simplifies development by avoiding external identity providers.

In production, replaced with **Azure Entra Identity**

Data Source
===========

The demo repository (`InMemoryDocumentRepository`) returns example data.

Example documents:

| Employee     | Department | Document       |
| ------------ | ---------- | -------------- |
| Alice Carter | HR         | Policy         |
| Bob Evans    | Finance    | Contract       |
| Carla Jones  | IT         | Handbook       |
| David Smith  | HR         | Benefits Guide |

Replace the repository implementation to connect to your real database.

* * *

Production Architecture Example
===============================

A typical production deployment might look like:

Users  
  │  
  ▼  
Internal Web App  
  │  
  ▼  
Azure App Service  
  │  
  ├── Azure SQL Database (document metadata)  
  │  
  └── Azure Blob Storage (PDF files)

The application retrieves metadata from the database and streams documents from blob storage through the API endpoint.

Security Considerations
=======================

Production practices:

* Uses Azure Entra for Identity

* Stores documents in private blob containers

* Streams files through the API instead of exposing direct blob URLs to clients

* Restricts Swagger UI to development environments

Feature Roadmap
===================

Planned enhancements:

* Azure SQL integration

* Azure Blob Storage integration

* role-based access control

Potential upgrades:

* document preview

* audit logging

* document upload capability

License
=======

Internal use only.
