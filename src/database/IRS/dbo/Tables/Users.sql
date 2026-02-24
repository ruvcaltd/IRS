CREATE TABLE [dbo].[Users] (
    [id]            INT            IDENTITY (1, 1) NOT NULL,
    [email]         NVARCHAR (255) NOT NULL,
    [password_hash] NVARCHAR (MAX) NOT NULL,
    [full_name]     NVARCHAR (255) NULL,
    [avatar_url]    NVARCHAR (500) NULL,
    [role_id]       INT            NOT NULL,
    [created_at]    DATETIME2 (7)  DEFAULT (getdate()) NULL,
    [is_deleted]    BIT            DEFAULT ((0)) NOT NULL,
    [deleted_at]    DATETIME2 (7)  NULL,
    PRIMARY KEY CLUSTERED ([id] ASC),
    FOREIGN KEY ([role_id]) REFERENCES [dbo].[Roles] ([id]),
    UNIQUE NONCLUSTERED ([email] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_Users_Email]
    ON [dbo].[Users]([email] ASC);

