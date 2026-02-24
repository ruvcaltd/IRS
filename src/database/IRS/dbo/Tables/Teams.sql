CREATE TABLE [dbo].[Teams] (
    [id]         INT            IDENTITY (1, 1) NOT NULL,
    [name]       NVARCHAR (255) NOT NULL,
    [created_at] DATETIME2 (7)  DEFAULT (getdate()) NULL,
    [is_deleted] BIT            DEFAULT ((0)) NOT NULL,
    [deleted_at] DATETIME2 (7)  NULL,
    PRIMARY KEY CLUSTERED ([id] ASC),
    UNIQUE NONCLUSTERED ([name] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_Teams_Name]
    ON [dbo].[Teams]([name] ASC);

