CREATE TABLE [dbo].[TeamMembers] (
    [user_id]             INT           NOT NULL,
    [team_id]             INT           NOT NULL,
    [team_role_id]        INT           NOT NULL,
    [status]              NVARCHAR (50) CONSTRAINT [DF_TeamMembers_Status] DEFAULT ('PENDING') NOT NULL,
    [approved_by_user_id] INT           NULL,
    [approved_at]         DATETIME2 (7) NULL,
    [created_at]          DATETIME2 (7) DEFAULT (getdate()) NULL,
    [is_deleted]          BIT           DEFAULT ((0)) NOT NULL,
    [deleted_at]          DATETIME2 (7) NULL,
    PRIMARY KEY CLUSTERED ([user_id] ASC, [team_id] ASC),
    CONSTRAINT [CK_TeamMembers_Status] CHECK ([status]='ACTIVE' OR [status]='PENDING'),
    FOREIGN KEY ([approved_by_user_id]) REFERENCES [dbo].[Users] ([id]),
    FOREIGN KEY ([team_id]) REFERENCES [dbo].[Teams] ([id]),
    FOREIGN KEY ([team_role_id]) REFERENCES [dbo].[TeamRoles] ([id]),
    FOREIGN KEY ([user_id]) REFERENCES [dbo].[Users] ([id])
);


GO
CREATE NONCLUSTERED INDEX [IX_TeamMembers_User_Status]
    ON [dbo].[TeamMembers]([user_id] ASC, [status] ASC) WHERE ([is_deleted]=(0));


GO
CREATE NONCLUSTERED INDEX [IX_TeamMembers_Team_Status]
    ON [dbo].[TeamMembers]([team_id] ASC, [status] ASC) WHERE ([is_deleted]=(0));


GO
CREATE NONCLUSTERED INDEX [IX_TeamMembers_Team_Status_Role]
    ON [dbo].[TeamMembers]([team_id] ASC, [status] ASC, [team_role_id] ASC) WHERE ([is_deleted]=(0));

