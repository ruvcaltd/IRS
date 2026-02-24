CREATE TABLE [dbo].[ResearchPages] (
    [id]                INT            IDENTITY (1, 1) NOT NULL,
    [team_id]           INT            NOT NULL,
    [security_figi]     NVARCHAR (50)  NOT NULL,
    [conviction_score]  INT            NULL,
    [fundamental_score] INT            NULL,
    [page_summary]      NVARCHAR (MAX) NULL,
    [created_at]        DATETIME2 (7)  DEFAULT (getdate()) NULL,
    [last_updated]      DATETIME2 (7)  NULL,
    [is_deleted]        BIT            DEFAULT ((0)) NOT NULL,
    [deleted_at]        DATETIME2 (7)  NULL,
    PRIMARY KEY CLUSTERED ([id] ASC),
    CONSTRAINT [CK_ResearchPages_ConvictionScore] CHECK ([conviction_score]>=(0) AND [conviction_score]<=(5)),
    CONSTRAINT [CK_ResearchPages_FundamentalScore] CHECK ([fundamental_score]>=(-3) AND [fundamental_score]<=(3)),
    FOREIGN KEY ([security_figi]) REFERENCES [dbo].[Securities] ([figi]),
    FOREIGN KEY ([team_id]) REFERENCES [dbo].[Teams] ([id]),
    CONSTRAINT [UQ_ResearchPages_Team_Security] UNIQUE NONCLUSTERED ([team_id] ASC, [security_figi] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_ResearchPages_Team_SecurityFigi]
    ON [dbo].[ResearchPages]([team_id] ASC, [security_figi] ASC) WHERE ([is_deleted]=(0));

