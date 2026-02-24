CREATE TABLE [dbo].[Comments] (
    [id]                INT            IDENTITY (1, 1) NOT NULL,
    [section_id]        INT            NOT NULL,
    [author_id]         INT            NULL,
    [author_type]       NVARCHAR (50)  NULL,
    [author_agent_name] NVARCHAR (100) NULL,
    [content]           NVARCHAR (MAX) NOT NULL,
    [created_at]        DATETIME2 (7)  DEFAULT (getdate()) NULL,
    [is_deleted]        BIT            DEFAULT ((0)) NOT NULL,
    [deleted_at]        DATETIME2 (7)  NULL,
    PRIMARY KEY CLUSTERED ([id] ASC),
    FOREIGN KEY ([author_id]) REFERENCES [dbo].[Users] ([id]),
    FOREIGN KEY ([section_id]) REFERENCES [dbo].[Sections] ([id])
);


GO
CREATE NONCLUSTERED INDEX [IX_Comments_SectionId_CreatedAt]
    ON [dbo].[Comments]([section_id] ASC, [created_at] ASC);

