CREATE TABLE [dbo].[LlmProviders] (
    [id]           INT            IDENTITY (1, 1) NOT NULL,
    [name]         NVARCHAR (50)  NOT NULL,
    [display_name] NVARCHAR (100) NOT NULL,
    [is_active]    BIT            DEFAULT ((1)) NOT NULL,
    [created_at]   DATETIME2 (7)  DEFAULT (getdate()) NULL,
    [updated_at]   DATETIME2 (7)  DEFAULT (getdate()) NULL,
    PRIMARY KEY CLUSTERED ([id] ASC),
    CONSTRAINT [UQ_LlmProviders_Name] UNIQUE NONCLUSTERED ([name] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Stores available LLM providers (OpenAI, Anthropic, etc.)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'LlmProviders';

