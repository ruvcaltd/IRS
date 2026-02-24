CREATE TABLE [dbo].[LlmModels] (
    [id]                        INT            IDENTITY (1, 1) NOT NULL,
    [provider_id]               INT            NOT NULL,
    [model_identifier]          NVARCHAR (100) NOT NULL,
    [display_name]              NVARCHAR (150) NOT NULL,
    [is_active]                 BIT            DEFAULT ((1)) NOT NULL,
    [supports_streaming]        BIT            DEFAULT ((1)) NOT NULL,
    [supports_function_calling] BIT            DEFAULT ((1)) NOT NULL,
    [supports_vision]           BIT            DEFAULT ((0)) NOT NULL,
    [max_tokens]                INT            NULL,
    [context_window]            INT            NULL,
    [created_at]                DATETIME2 (7)  DEFAULT (getdate()) NULL,
    [updated_at]                DATETIME2 (7)  DEFAULT (getdate()) NULL,
    PRIMARY KEY CLUSTERED ([id] ASC),
    FOREIGN KEY ([provider_id]) REFERENCES [dbo].[LlmProviders] ([id]),
    CONSTRAINT [UQ_LlmModels_ProviderModel] UNIQUE NONCLUSTERED ([provider_id] ASC, [model_identifier] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_LlmModels_ProviderId]
    ON [dbo].[LlmModels]([provider_id] ASC) WHERE ([is_active]=(1));


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Stores available LLM models for each provider with capabilities and limits', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'LlmModels';

