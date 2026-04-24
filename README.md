# Employee Documents Viewer

Employee Documents Viewer is an internal ASP.NET Core web application for HR users to:

- choose a company,
- search and sort an employee document catalog stored in SQL, and
- open the underlying file stream from Azure Blob Storage.

The application is intentionally split into **metadata query in SQL** and **content retrieval from Blob Storage**.

---

## What this app does

1. Renders a Razor Pages UI at `/documents`.
2. Calls a FastEndpoints API (`POST /api/documents/list`) for paging/filtering/sorting.
3. Reads document metadata from SQL (`HR.EmployeeDocumentCatalog` + `HR.EmployeeDocumentsLookup`).
4. Opens files through `GET /api/documents/open/{companyKey}?blobName=...` by streaming blobs from the `hrdocs` container.

---

## Tech stack

- .NET 10 (`net10.0`)
- ASP.NET Core Razor Pages
- FastEndpoints + FastEndpoints.Swagger
- EF Core SQL Server provider
- Azure Blob Storage SDK
- Microsoft Identity Web (OpenID Connect / Entra ID)
- Azure Monitor OpenTelemetry exporter

---

## Runtime architecture

```text
Browser (/documents)
  -> Razor Page + vanilla JS table UI
  -> FastEndpoints API
  -> SqlDocumentRepository
  -> SQL metadata tables/views

Document open request
  -> FastEndpoints API
  -> SqlDocumentRepository
  -> Azure Blob Storage (container: hrdocs)
```

### Key behavior

- **No blob listing for search.** Search runs against SQL only.
- **Blob access is on-demand only** when a user clicks Open.
- **Per-company connection resolution** is handled from configuration.

---

## Repository structure (current)

```text
EmployeeDocumentsViewer/
  Configuration/
    CompanyConnectionOptions.cs
    CompanyConnectionStringResolver.cs
    DocumentsPageOptions.cs
    StorageOptions.cs
  Features/
    Company.cs
    Documents/
      Data/
        DocumentCatalogDbContext.cs
        Entities/
      List/
        Endpoint.cs
        Request.cs
        Response.cs
        DocumentReadRow.cs
      Open/
        Endpoint.cs
        Request.cs
      SqlDocumentRepository.cs
      DocumentSortParser.cs
      DocumentSortColumn.cs
  Pages/
    Documents/
      Index.cshtml
      Index.cshtml.cs
  Scripts/
    *.sql
  Program.cs
```

---

## Configuration

Configuration is primarily in `appsettings.json` (with environment overrides from `appsettings.Development.json` and user secrets/environment variables).

### 1) Company connection mapping

Each company requires:

- `ConnectionString` (SQL)
- `BlobStorageConnectionString` (Blob)
- `DisplayName` (UI label)

Path:

```json
CompanyConnections:Companies:{CompanyKey}
```

Supported company keys are currently the `Company` enum values:

- `CII`
- `CSI`
- `DSI`
- `DSN`

### 2) Authentication (Microsoft Entra ID/AzureAd)

The app expects full `AzureAd` settings:

- `Instance`
- `TenantId`
- `ClientId`
- `ClientSecret`
- `CallbackPath`

If AzureAd settings are incomplete, the app registers a local cookie scheme only, and authorization is forced to deny-all.

### 3) Authorization group

`Authorization:HREmployeeDocumentsGroupId` must be set.

The policy `HREmployeeDocumentsOnly` requires:

- authenticated user
- `groups` claim containing that group ID

If missing, app runs in deny-all mode.

### 4) Telemetry

Set either:

- `ConnectionStrings:ApplicationInsights`, or
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

The connection string is validated at startup; invalid values disable telemetry.

### 5) Documents page text

`DocumentsPage` controls UI title/description/message shown on `/documents`.

---

## Database model used by the app

`DocumentCatalogDbContext` uses:

- `HR.EmployeeDocumentCatalog` (table)

Query behavior in `SqlDocumentRepository.SearchAsync`:

- filters to `IsDeleted = 0`
- database selected by selected `CompanyKey`
- reads employee metadata directly from the catalog table
- applies optional search over blob/document/employee/department (+ numeric employee ID match)
- applies server-side sorting
- applies paging with max page size clamp of 500

Returned row model includes:

- BlobName
- EmployeeId / EmployeeName
- Department
- DocumentType
- UpdatedUtc (fallback to BlobLastModifiedUtc)
- Active

---

## HTTP endpoints

## `POST /api/documents/list`

Request JSON:

```json
{
  "draw": 1,
  "companyKey": "CII",
  "start": 0,
  "length": 50,
  "searchTerm": "w2",
  "sortColumn": "updatedUtc",
  "sortDirection": "desc"
}
```

Sort columns accepted by parser:

- `employeeId`
- `employee`
- `department`
- `documentType`
- `updatedUtc` (default)

Response shape:

```json
{
  "draw": 1,
  "recordsTotal": 1234,
  "recordsFiltered": 52,
  "data": [
    {
      "blobName": "...",
      "employeeId": 123,
      "employeeName": "Doe, Jane",
      "department": "HR",
      "documentType": "W-2",
      "updatedUtc": "2026-01-10T15:22:00+00:00"
    }
  ]
}
```

## `GET /api/documents/open/{companyKey}?blobName=...`

Behavior:

- 400 for invalid company key
- 400 when `blobName` is missing
- 404 when blob does not exist
- 200 stream result with range processing enabled when found

---

## Local development

## Prerequisites

- .NET SDK 10
- Access to target SQL databases
- Access to target Azure Blob Storage accounts/containers
- Azure app registration secrets (for full auth flow)

## Setup

1. Populate configuration values in user secrets or environment variables (recommended), including:
   - `CompanyConnections`
   - `AzureAd`
   - `Authorization:HREmployeeDocumentsGroupId`
2. Restore/build:

```bash
dotnet restore
dotnet build
```

3. Run:

```bash
dotnet run
```

Default local URLs are in `Properties/launchSettings.json`.

Open:

- App: `https://localhost:7043/documents`
- Swagger (Development only): `https://localhost:7043/swagger`
- Debug claims endpoint (Development only): `https://localhost:7043/debug/claims`
- Health endpoint (Development only): `https://localhost:7043/health`

---

## Security notes

- App is intended for authenticated HR users in a specific Entra group.
- If required auth settings are missing, fallback policy blocks requests (deny-all).
- Files are proxied through the app (no direct blob URL exposure in UI).
- Non-development environments enable exception handler + HSTS.

---

## SQL scripts folder

`Scripts/` contains SQL artifacts related to catalog search objects and migration support (views/procedures/indexes). These scripts are operational assets and are not automatically executed by app startup.

---

## Known implementation notes

- `Storage:DocumentsContainerName` exists in configuration options, but `SqlDocumentRepository` currently uses a hard-coded container name (`hrdocs`).
- The project references EF Core `10.0.0-preview` packages, so SDK/package compatibility should be validated in CI/build agents.

---

## License

Internal use only.
