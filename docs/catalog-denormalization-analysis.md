# Catalog Denormalization Analysis

## Current viewer read path

The viewer currently reads from `HR.EmployeeDocumentCatalog` and left-joins to `HR.EmployeeDocumentsLookup` by `EmployeeId` to project employee-facing fields (`Employee`, `Department`, `Active`).

## Proposed change

Denormalize lookup fields into `HR.EmployeeDocumentCatalog` during indexing/backfill so the viewer can read from one table and avoid join work.

## Assessment

This is directionally strong (the product tolerates eventual consistency) for employee attributes.

### Benefits

- Simpler read path in the web app and EF query.
- Lower query CPU/IO by removing runtime join on each request.
- Fewer cross-object dependencies (`HR.EmployeeDocumentsLookup` no longer required by the app).
- Predictable indexing strategy on a single table for search/sort columns.
- Improved query performance and user experience.

### Risks and design considerations

- Employee attributes (`NameLastFirst`, `HomeDepartment`, `Active`) change independently of documents.
  - If stored in catalog rows, values can become stale, however refresh logic exists in EmployeeDocumentsIndexer Function App.
- `EmployeeDocumentsLookup` currently encapsulates business logic (company-specific SQL, active ranking for CII).
  - That logic must move into indexer pipelines or upstream staging SQL.
- Storage amplification: repeated employee metadata per document row.
- Backfill and ongoing sync strategy needed when HR data changes but no blob change occurs.

## Recommended approach

1. Add denormalized columns to `HR.EmployeeDocumentCatalog`:
   - `EmployeeNameLastFirst`, `HomeDepartment`, `EmployeeActive`
   - Optional: `EmployeeLookupLastSyncedUtc` for observability.
2. Populate these fields during initial backfill from the same source used by `EmployeeDocumentsLookup`.
3. Add an event-driven HR-delta refresh job in indexer(s) to update catalog rows for changed employees.
4. Update viewer read query to use only `EmployeeDocumentCatalog`.
5. Keep `HR.EmployeeDocumentsLookup` temporarily for validation, then deprecate.

## Migration notes

- Use a phased rollout: dual-read/compare in non-prod, then cut over.
- Add/adjust indexes for common filters/sorts on denormalized columns.
- Define and monitor staleness SLO (e.g., employee metadata freshness <= 24h).

## Conclusion

The proposal is an appropriate optimization and architecture simplification for the viewer, with the key tradeoff that consistency moves from query-time to pipeline-time. With operationalized event-driven metadata refresh (indexer app) and acceptance of eventual consistency, this is likely a net win.
