CREATE TABLE [dbo].[Sections] (
    [id]                   INT            IDENTITY (1, 1) NOT NULL,
    [research_page_id]     INT            NOT NULL,
    [title]                NVARCHAR (255) NOT NULL,
    [fundamental_score]    INT            NULL,
    [conviction_score]     INT            NULL,
    [section_summary]      NVARCHAR (MAX) NULL,
    [ai_generated_content] NVARCHAR (MAX) NULL,
    [is_deleted]           BIT            DEFAULT ((0)) NOT NULL,
    [deleted_at]           DATETIME2 (7)  NULL,
    PRIMARY KEY CLUSTERED ([id] ASC),
    CONSTRAINT [CK_Sections_ConvictionScore] CHECK ([conviction_score]>=(0) AND [conviction_score]<=(5)),
    CONSTRAINT [CK_Sections_FundamentalScore] CHECK ([fundamental_score]>=(-3) AND [fundamental_score]<=(3)),
    FOREIGN KEY ([research_page_id]) REFERENCES [dbo].[ResearchPages] ([id])
);


GO
CREATE NONCLUSTERED INDEX [IX_Sections_ResearchPageId]
    ON [dbo].[Sections]([research_page_id] ASC) WHERE ([is_deleted]=(0));

