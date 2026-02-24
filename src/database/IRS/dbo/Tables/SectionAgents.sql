CREATE TABLE [dbo].[SectionAgents] (
    [id]              INT           IDENTITY (1, 1) NOT NULL,
    [section_id]      INT           NOT NULL,
    [agent_id]        INT           NOT NULL,
    [is_enabled]      BIT           DEFAULT ((1)) NOT NULL,
    [last_run_status] NVARCHAR (50) NULL,
    [last_run_at]     DATETIME2 (7) NULL,
    PRIMARY KEY CLUSTERED ([id] ASC),
    CONSTRAINT [CK_SectionAgents_LastRunStatus] CHECK ([last_run_status]='Failed' OR [last_run_status]='Succeeded' OR [last_run_status]='Running' OR [last_run_status]='Queued'),
    FOREIGN KEY ([agent_id]) REFERENCES [dbo].[Agents] ([id]),
    FOREIGN KEY ([section_id]) REFERENCES [dbo].[Sections] ([id]),
    UNIQUE NONCLUSTERED ([section_id] ASC, [agent_id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_SectionAgents_SectionId]
    ON [dbo].[SectionAgents]([section_id] ASC);

