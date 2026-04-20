-- Run in CII company database

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER VIEW [HR].[EmployeeDocumentsLookup]
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