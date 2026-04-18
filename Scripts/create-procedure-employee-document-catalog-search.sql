-- CSI database CsiSql
-- DSI database DsiSql
-- DSN database DsnSql

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- [Common].[usp_EmployeeDocumentCatalog_Search]

CREATE OR ALTER PROCEDURE [HR].[EmployeeDocumentCatalogSearch]
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
    FROM HR.EmployeeDocumentCatalogSearchBase;

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
            FROM HR.EmployeeDocumentCatalogSearchBase
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
            FROM HR.EmployeeDocumentCatalogSearchBase
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
