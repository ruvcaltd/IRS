CREATE TABLE [dbo].[AgentRuns] (
    [id]                     INT            IDENTITY (1, 1) NOT NULL,
    [research_page_agent_id] INT            NULL,
    [section_id]             INT            NULL,
    [status]                 NVARCHAR (50)  NOT NULL,
    [started_at]             DATETIME2 (7)  DEFAULT (getdate()) NULL,
    [completed_at]           DATETIME2 (7)  NULL,
    [output]                 NVARCHAR (MAX) NULL,
    [error]                  NVARCHAR (MAX) NULL,
    [section_agent_id]       INT            NULL,
    PRIMARY KEY CLUSTERED ([id] ASC),
    CONSTRAINT [CK_AgentRuns_Status] CHECK ([status]='Failed' OR [status]='Succeeded' OR [status]='Running' OR [status]='Queued'),
    FOREIGN KEY ([research_page_agent_id]) REFERENCES [dbo].[ResearchPageAgents] ([id]),
    FOREIGN KEY ([section_id]) REFERENCES [dbo].[Sections] ([id]),
    CONSTRAINT [FK_AgentRuns_SectionAgent] FOREIGN KEY ([section_agent_id]) REFERENCES [dbo].[SectionAgents] ([id])
);




GO
CREATE NONCLUSTERED INDEX [IX_AgentRuns_Status_StartedAt]
    ON [dbo].[AgentRuns]([status] ASC, [started_at] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AgentRuns_SectionAgentId]
    ON [dbo].[AgentRuns]([section_agent_id] ASC);

