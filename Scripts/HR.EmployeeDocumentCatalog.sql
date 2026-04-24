if object_id('HR.EmployeeDocumentCatalog', 'U') is not null
begin
    drop table HR.EmployeeDocumentCatalog;
end
go

create table HR.EmployeeDocumentCatalog
(
    Id                      bigint              not null identity(1,1),
    BlobName                nvarchar(512)       not null,
    BlobNameHash            varbinary(32)       not null,
    EmployeeId              int                 not null,
    DocumentTypeToken       nvarchar(200)       not null,
    DocumentTypeDisplay     nvarchar(200)       not null,
    EmployeeName            nvarchar(200)       null,
    HomeDepartment          nvarchar(200)       null,
    EmployeeActive          bit                 not null
        constraint DF_EmployeeDocumentCatalog_EmployeeActive default (0),
    EmployeeLookupLastSyncedUtc datetimeoffset(7) null,
    UpdatedUtc              datetimeoffset(7)   null,
    BlobLastModifiedUtc     datetimeoffset(7)   null,
    ContentType             nvarchar(200)       null,
    BlobETag                nvarchar(128)       null,
    IsDeleted               bit                 not null
        constraint DF_EmployeeDocumentCatalog_IsDeleted default (0),
    LastIndexedUtc          datetimeoffset(7)   not null,
    constraint PK_EmployeeDocumentCatalog
        primary key clustered (Id)
);
go

create unique nonclustered index UX_EmployeeDocumentCatalog_Company_BlobHash
    on HR.EmployeeDocumentCatalog (BlobNameHash);
go

create index IX_EmployeeDocumentCatalog_Company_Employee
    on HR.EmployeeDocumentCatalog (EmployeeId);
go

create index IX_EmployeeDocumentCatalog_Company_DocumentType
    on HR.EmployeeDocumentCatalog (DocumentTypeDisplay);
go

create index IX_EmployeeDocumentCatalog_Company_UpdatedUtc
    on HR.EmployeeDocumentCatalog (UpdatedUtc desc);
go

create index IX_EmployeeDocumentCatalog_Company_IsDeleted
    on HR.EmployeeDocumentCatalog (IsDeleted);
go
