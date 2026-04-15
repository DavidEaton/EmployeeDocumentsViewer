-- Run in each company database. This script is designed for safety and validation before cutover.

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/*=====================================================================
  1. SUPPORTING INDEXES
  ---------------------------------------------------------------------
  These are additive and safe to create before any object cutover.
=====================================================================*/

IF NOT EXISTS
(
    SELECT 1
FROM sys.indexes
WHERE name = N'IX_Employment_PartyId_Active_Hired_EmploymentId'
    AND object_id = OBJECT_ID(N'dbo.Employment')
)
BEGIN
    CREATE INDEX IX_Employment_PartyId_Active_Hired_EmploymentId
    ON dbo.Employment (PartyId, Active, Hired DESC, EmploymentId DESC)
    INCLUDE (HomeIoId);
END
GO

IF NOT EXISTS
(
    SELECT 1
FROM sys.indexes
WHERE name = N'IX_Termination_EmploymentId_TerminationDateDesc'
    AND object_id = OBJECT_ID(N'dbo.Termination')
)
BEGIN
    CREATE INDEX IX_Termination_EmploymentId_TerminationDateDesc
    ON dbo.Termination (EmploymentId, TerminationDate DESC, TerminationId DESC);
END
GO

IF NOT EXISTS
(
    SELECT 1
FROM sys.indexes
WHERE name = N'IX_EmployeeDocumentCatalog_SearchBase_v2'
    AND object_id = OBJECT_ID(N'Common.EmployeeDocumentCatalog')
)
BEGIN
    CREATE INDEX IX_EmployeeDocumentCatalog_SearchBase_v2
    ON Common.EmployeeDocumentCatalog (IsDeleted, EmployeeId, UpdatedUtc DESC)
    INCLUDE (BlobName, DocumentTypeDisplay, BlobLastModifiedUtc, ContentType);
END
GO


/*=====================================================================
  2. V2 VIEWS
  ---------------------------------------------------------------------
  These are created side-by-side for safe validation before cutover.
=====================================================================*/

CREATE OR ALTER VIEW [Common].[EmployeeEeDocsLookup_v2]
AS
    WITH
        RankedEmployment
        AS
        (
            SELECT
                E.EmploymentId,
                E.PartyId,
                E.HomeIoId,
                E.Hired,
                E.Active,
                rn = ROW_NUMBER() OVER
        (
            PARTITION BY E.PartyId
            ORDER BY
                CASE WHEN E.Active = 1 THEN 0 ELSE 1 END,
                E.Hired DESC,
                E.EmploymentId DESC
        )
            FROM dbo.Employment E
        )
    SELECT
        re.PartyId AS Id,
        P.NameLastFirst,
        P.NameFirstLast,
        re.Active,
        re.Hired,
        H.IoName AS HomeDepartment,
        N'n/a' AS HomeArea,
        re.HomeIoId AS DepartmentId,
        -1 AS AreaId
    FROM RankedEmployment re
        INNER JOIN dbo.Person P
        ON P.PartyId = re.PartyId
        INNER JOIN dbo.InternalOrganization H
        ON H.PartyId = re.HomeIoId
    WHERE re.rn = 1;
GO

CREATE OR ALTER VIEW [HR].[LatestTerminationByParty_v2]
AS
    WITH
        RankedTerminations
        AS
        (
            SELECT
                E.PartyId,
                T.TerminationDate,
                rn = ROW_NUMBER() OVER
        (
            PARTITION BY E.PartyId
            ORDER BY
                T.TerminationDate DESC,
                T.TerminationId DESC
        )
            FROM dbo.Termination T
                INNER JOIN dbo.Employment E
                ON E.EmploymentId = T.EmploymentId
        )
    SELECT
        PartyId,
        TerminationDate
    FROM RankedTerminations
    WHERE rn = 1;
GO

CREATE OR ALTER VIEW [Common].[vw_EmployeeDocumentCatalogSearchBase_v2]
AS
    SELECT
        d.BlobName,
        d.EmployeeId,
        emp.NameLastFirst AS Employee,
        emp.HomeDepartment AS Department,
        d.DocumentTypeDisplay AS DocumentType,
        YEAR(COALESCE(d.UpdatedUtc, d.BlobLastModifiedUtc)) AS [Year],
        COALESCE(d.UpdatedUtc, d.BlobLastModifiedUtc) AS UpdatedUtc,
        d.ContentType,
        emp.Active,
        term.TerminationDate
    FROM Common.EmployeeDocumentCatalog d
        INNER JOIN Common.EmployeeEeDocsLookup_v2 emp
        ON emp.Id = d.EmployeeId
        LEFT JOIN HR.LatestTerminationByParty_v2 term
        ON term.PartyId = emp.Id
    WHERE d.IsDeleted = 0;
GO


/*=====================================================================
  3. REWRITTEN PROC
  ---------------------------------------------------------------------
  Key changes:
  - Removes #Filtered temp table materialization.
  - Uses the v2 base view.
  - Preserves the 3 result sets expected by the C# code:
      1) TotalCount
      2) FilteredCount
      3) Paged rows
=====================================================================*/

CREATE OR ALTER PROCEDURE [Common].[usp_EmployeeDocumentCatalog_Search]
    @SearchTerm      nvarchar(200) = NULL,
    @SortColumn      sysname,
    @SortDescending  bit,
    @Start           int,
    @Length          int
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NormalizedSearchTerm nvarchar(200) =
        NULLIF(LTRIM(RTRIM(@SearchTerm)), N'');

    DECLARE @HasSearch bit =
        CASE WHEN @NormalizedSearchTerm IS NULL THEN 0 ELSE 1 END;

    /*-------------------------------------------------------------
      Result set 1: total count (unfiltered)
    -------------------------------------------------------------*/
    SELECT COUNT(*) AS TotalCount
    FROM Common.vw_EmployeeDocumentCatalogSearchBase_v2;

    /*-------------------------------------------------------------
      Result set 2: filtered count
    -------------------------------------------------------------*/
    ;WITH
        Filtered
        AS
        (
            SELECT
                BlobName,
                EmployeeId,
                Employee,
                Department,
                DocumentType,
                [Year],
                UpdatedUtc,
                ContentType,
                Active,
                TerminationDate
            FROM Common.vw_EmployeeDocumentCatalogSearchBase_v2
            WHERE
            @HasSearch = 0
                OR Employee LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR Department LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR DocumentType LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR BlobName LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR CAST(EmployeeId AS nvarchar(20)) LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR CAST([Year] AS nvarchar(10)) LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR CASE WHEN Active = 1 THEN N'Active' ELSE N'Terminated' END LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR CONVERT(nvarchar(10), TerminationDate, 23) LIKE N'%' + @NormalizedSearchTerm + N'%'
        )
    SELECT COUNT(*) AS FilteredCount
    FROM Filtered;

    /*-------------------------------------------------------------
      Result set 3: page rows
      Uses CASE-based ORDER BY to avoid string-built dynamic SQL.
      Always ends with stable fallback ordering.
    -------------------------------------------------------------*/
    ;WITH
        Filtered
        AS
        (
            SELECT
                BlobName,
                EmployeeId,
                Employee,
                Department,
                DocumentType,
                [Year],
                UpdatedUtc,
                ContentType,
                Active,
                TerminationDate
            FROM Common.vw_EmployeeDocumentCatalogSearchBase_v2
            WHERE
            @HasSearch = 0
                OR Employee LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR Department LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR DocumentType LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR BlobName LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR CAST(EmployeeId AS nvarchar(20)) LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR CAST([Year] AS nvarchar(10)) LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR CASE WHEN Active = 1 THEN N'Active' ELSE N'Terminated' END LIKE N'%' + @NormalizedSearchTerm + N'%'
                OR CONVERT(nvarchar(10), TerminationDate, 23) LIKE N'%' + @NormalizedSearchTerm + N'%'
        )
    SELECT
        BlobName,
        EmployeeId,
        Employee,
        Department,
        DocumentType,
        [Year],
        UpdatedUtc,
        ContentType,
        Active,
        TerminationDate
    FROM Filtered
    ORDER BY
        CASE WHEN @SortColumn = N'EmployeeId' AND @SortDescending = 0 THEN EmployeeId END ASC,
        CASE WHEN @SortColumn = N'EmployeeId' AND @SortDescending = 1 THEN EmployeeId END DESC,

        CASE WHEN @SortColumn = N'Employee' AND @SortDescending = 0 THEN Employee END ASC,
        CASE WHEN @SortColumn = N'Employee' AND @SortDescending = 1 THEN Employee END DESC,

        CASE WHEN @SortColumn = N'Department' AND @SortDescending = 0 THEN Department END ASC,
        CASE WHEN @SortColumn = N'Department' AND @SortDescending = 1 THEN Department END DESC,

        CASE WHEN @SortColumn = N'DocumentType' AND @SortDescending = 0 THEN DocumentType END ASC,
        CASE WHEN @SortColumn = N'DocumentType' AND @SortDescending = 1 THEN DocumentType END DESC,

        CASE WHEN @SortColumn = N'Year' AND @SortDescending = 0 THEN [Year] END ASC,
        CASE WHEN @SortColumn = N'Year' AND @SortDescending = 1 THEN [Year] END DESC,

        CASE WHEN @SortColumn = N'Active' AND @SortDescending = 0 THEN Active END ASC,
        CASE WHEN @SortColumn = N'Active' AND @SortDescending = 1 THEN Active END DESC,

        CASE WHEN @SortColumn = N'TerminationDate' AND @SortDescending = 0 THEN TerminationDate END ASC,
        CASE WHEN @SortColumn = N'TerminationDate' AND @SortDescending = 1 THEN TerminationDate END DESC,

        UpdatedUtc DESC,
        Employee ASC
    OFFSET @Start ROWS
    FETCH NEXT @Length ROWS ONLY;
END
GO


/*=====================================================================
  4. VALIDATION QUERIES
  ---------------------------------------------------------------------
  Run these BEFORE any optional cutover/cleanup.
=====================================================================*/

PRINT 'Validation 1: row counts old vs v2';
SELECT COUNT(*) AS OldCount
FROM Common.vw_EmployeeDocumentCatalogSearchBase;

SELECT COUNT(*) AS NewCount
FROM Common.vw_EmployeeDocumentCatalogSearchBase_v2;
GO

PRINT 'Validation 2: duplicate check old view';
SELECT
    BlobName,
    EmployeeId,
    COUNT(*) AS DuplicateCount
FROM Common.vw_EmployeeDocumentCatalogSearchBase
GROUP BY BlobName, EmployeeId
HAVING COUNT(*) > 1
ORDER BY DuplicateCount DESC, EmployeeId, BlobName;
GO

PRINT 'Validation 3: duplicate check v2 view';
SELECT
    BlobName,
    EmployeeId,
    COUNT(*) AS DuplicateCount
FROM Common.vw_EmployeeDocumentCatalogSearchBase_v2
GROUP BY BlobName, EmployeeId
HAVING COUNT(*) > 1
ORDER BY DuplicateCount DESC, EmployeeId, BlobName;
GO

PRINT 'Validation 4: sample compare old vs v2';
SELECT TOP (50)
    *
FROM Common.vw_EmployeeDocumentCatalogSearchBase
ORDER BY UpdatedUtc DESC, Employee;

SELECT TOP (50)
    *
FROM Common.vw_EmployeeDocumentCatalogSearchBase_v2
ORDER BY UpdatedUtc DESC, Employee;
GO

PRINT 'Validation 5: proc smoke tests';
EXEC Common.usp_EmployeeDocumentCatalog_Search
    @SearchTerm = NULL,
    @SortColumn = N'Employee',
    @SortDescending = 0,
    @Start = 0,
    @Length = 10;

EXEC Common.usp_EmployeeDocumentCatalog_Search
    @SearchTerm = N'smith',
    @SortColumn = N'Employee',
    @SortDescending = 0,
    @Start = 0,
    @Length = 10;

EXEC Common.usp_EmployeeDocumentCatalog_Search
    @SearchTerm = N'2024',
    @SortColumn = N'Year',
    @SortDescending = 1,
    @Start = 0,
    @Length = 10;
GO


/*=====================================================================
  5. OPTIONAL PERFORMANCE TESTING
  ---------------------------------------------------------------------
  Run in SSMS with Actual Execution Plan enabled.
=====================================================================*/

SET STATISTICS IO ON;
SET STATISTICS TIME ON;
GO

EXEC Common.usp_EmployeeDocumentCatalog_Search
    @SearchTerm = NULL,
    @SortColumn = N'Employee',
    @SortDescending = 0,
    @Start = 0,
    @Length = 10;

EXEC Common.usp_EmployeeDocumentCatalog_Search
    @SearchTerm = N'smith',
    @SortColumn = N'Employee',
    @SortDescending = 0,
    @Start = 0,
    @Length = 10;
GO

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
GO


/*=====================================================================
  6. OPTIONAL CUTOVER / CLEANUP
  ---------------------------------------------------------------------
  Do NOT run this immediately.
  First validate counts, duplicates, and performance.
=====================================================================*/

/*
-- If you later decide v2 is correct and want canonical names instead of
-- side-by-side objects, do that in a separate deployment window.

-- Example future cleanup approach:
-- 1. Keep proc as-is if it already points to _v2.
-- 2. Leave old views in place for one release cycle.
-- 3. After confidence is high, drop old views or replace callers.

-- I do NOT recommend automatic drop/rename in the same first deployment.
*/


/*=====================================================================
  7. ROLLBACK
  ---------------------------------------------------------------------
  Safe rollback leaves original objects untouched except the proc body.
=====================================================================*/

/*
-- If the new proc must be reverted quickly, recreate the old proc body
-- from source control / your original script.

-- To remove additive indexes if desired:
IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Employment_PartyId_Active_Hired_EmploymentId'
      AND object_id = OBJECT_ID(N'dbo.Employment')
)
BEGIN
    DROP INDEX IX_Employment_PartyId_Active_Hired_EmploymentId ON dbo.Employment;
END
GO

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Termination_EmploymentId_TerminationDateDesc'
      AND object_id = OBJECT_ID(N'dbo.Termination')
)
BEGIN
    DROP INDEX IX_Termination_EmploymentId_TerminationDateDesc ON dbo.Termination;
END
GO

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_EmployeeDocumentCatalog_SearchBase_v2'
      AND object_id = OBJECT_ID(N'Common.EmployeeDocumentCatalog')
)
BEGIN
    DROP INDEX IX_EmployeeDocumentCatalog_SearchBase_v2 ON Common.EmployeeDocumentCatalog;
END
GO

-- To remove v2 views:
IF OBJECT_ID(N'Common.vw_EmployeeDocumentCatalogSearchBase_v2', N'V') IS NOT NULL
    DROP VIEW Common.vw_EmployeeDocumentCatalogSearchBase_v2;
GO

IF OBJECT_ID(N'HR.LatestTerminationByParty_v2', N'V') IS NOT NULL
    DROP VIEW HR.LatestTerminationByParty_v2;
GO

IF OBJECT_ID(N'Common.EmployeeEeDocsLookup_v2', N'V') IS NOT NULL
    DROP VIEW Common.EmployeeEeDocsLookup_v2;
GO
*/