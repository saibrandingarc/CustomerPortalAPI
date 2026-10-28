-- Run against database: barcportalTest
-- Creates DeliverableFileApprovals table for file-level approve/reject tracking

IF OBJECT_ID(N'[dbo].[DeliverableFileApprovals]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DeliverableFileApprovals] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [DeliverableId] NVARCHAR(50) NOT NULL,
        [FileId] NVARCHAR(255) NOT NULL,
        [FileName] NVARCHAR(500) NOT NULL,
        [FileUrl] NVARCHAR(2048) NULL,
        [Notes] NVARCHAR(2000) NULL,
        [Status] NVARCHAR(50) NOT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        [UpdatedAtUtc] DATETIME2 NULL,
        CONSTRAINT [PK_DeliverableFileApprovals] PRIMARY KEY ([Id])
    );

    CREATE INDEX [IX_DeliverableFileApprovals_DeliverableId_FileId]
        ON [dbo].[DeliverableFileApprovals] ([DeliverableId], [FileId]);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610165000_AddDeliverableFileApprovals'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610165000_AddDeliverableFileApprovals', N'9.0.10');
END
GO
