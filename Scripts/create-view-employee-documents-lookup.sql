-- CSI database CsiSql
-- DSI database DsiSql
-- DSN database DsnSql

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER VIEW [HR].[EmployeeDocumentsLookup]
AS
    SELECT DISTINCT
        P.PartyID AS Id,
        NameLastFirst,
        NameFirstLast,
        CAST(
		CASE WHEN (P.PartyID IN
						(SELECT PartyIDFrom
        FROM PartyRelationship
        WHERE (PartyRoleTypeID = 3)
            AND (PartyIDTo = 1055707269)
            AND [Active] = 1)
		)
		THEN 1
		ELSE 0 END
	AS bit) AS 'Active',

        (SELECT TOP(1)
            ValidFrom
        FROM PartyRelationship
        WHERE (PartyRoleTypeID = 3)
            AND (PartyIDTo = 1055707269)
            AND (PartyIDFrom = P.PartyID)
        ORDER BY ValidFrom DESC)
	AS Hired,

        D.DepartmentName AS HomeDepartment,
        A.AreaName AS HomeArea,
        H.DepartmentID,
        D.AreaID,
        HR.EmployeeStatusAbbr(P.PartyID) AS StatusAbbr,
        H.Created,
        H.Edited

    FROM
        Person P
        INNER JOIN
        PartyRelationship R
        ON
		P.PartyID = R.PartyIDFrom

        LEFT OUTER JOIN
        HomeDepartment H
        ON
		P.PartyID = H.PartyID
        LEFT OUTER JOIN
        Department D
        ON
		H.DepartmentID = D.PartyID
        LEFT OUTER JOIN
        Area A
        ON
		D.AreaID = A.AreaID
    WHERE
		(PartyRoleTypeID = 3)
        AND
        (PartyIDTo = 1055707269);
GO
