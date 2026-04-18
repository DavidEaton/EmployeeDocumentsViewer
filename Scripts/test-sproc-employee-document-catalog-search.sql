SET STATISTICS IO ON;
SET STATISTICS TIME ON;
GO

EXEC [HR].[EmployeeDocumentCatalogSearch]
    @SearchTerm = NULL,
    @SortColumn = N'Employee',
    @SortDescending = 0,
    @Start = 0,
    @Length = 10;
GO

EXEC [HR].[EmployeeDocumentCatalogSearch]
    @SearchTerm = N'smith',
    @SortColumn = N'Employee',
    @SortDescending = 0,
    @Start = 0,
    @Length = 10;
GO

EXEC [HR].[EmployeeDocumentCatalogSearch]
    @SearchTerm = N'0',
    @SortColumn = N'Year',
    @SortDescending = 1,
    @Start = 0,
    @Length = 10;
GO

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
GO