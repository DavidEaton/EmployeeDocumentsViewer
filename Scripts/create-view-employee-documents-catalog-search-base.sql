-- CSI database CsiSql
-- DSI database DsiSql
-- DSN database DsnSql

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- CREATE OR ALTER VIEW  [Common].[vw_EmployeeDocumentCatalogSearchBase]
-- create-view-employee-documents-catalog-search-base.sql
CREATE OR ALTER VIEW  [HR].[EmployeeDocumentCatalogSearchBase]
as
    select
        d.BlobName,
        d.EmployeeId,
        emp.NameLastFirst as Employee,
        emp.HomeDepartment as Department,
        d.DocumentTypeDisplay as DocumentType,
        0 as [Year],
        -- year(coalesce(d.UpdatedUtc, d.BlobLastModifiedUtc)) as [Year],
        coalesce(d.UpdatedUtc, d.BlobLastModifiedUtc) as UpdatedUtc,
        d.ContentType,
        emp.Active,
        0 as TerminationDate
    -- term.TerminationDate
    from Common.EmployeeDocumentCatalog d
        inner join HR.EmployeeDocumentsLookup emp
        on emp.Id = d.EmployeeId
    -- outer apply
    -- (
    --     select top (1)
    --         tm.TerminationDate
    --     from HR.Terminations tm
    --     where tm.PartyID = emp.Id
    --     order by tm.TerminationDate desc
    -- ) term
    where d.IsDeleted = 0;
GO
