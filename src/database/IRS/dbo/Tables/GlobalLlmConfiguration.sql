CREATE TABLE [dbo].[GlobalLlmConfiguration] (
    [id]                       INT             DEFAULT ((1)) NOT NULL,
    [global_model_id]          INT             NULL,
    [encrypted_global_api_key] VARBINARY (MAX) NULL,
    [updated_at]               DATETIME2 (7)   DEFAULT (getdate()) NULL,
    [updated_by_user_id]       INT             NULL,
    PRIMARY KEY CLUSTERED ([id] ASC),
    CONSTRAINT [CK_GlobalLlmConfiguration_Singleton] CHECK ([id]=(1)),
    FOREIGN KEY ([global_model_id]) REFERENCES [dbo].[LlmModels] ([id]),
    FOREIGN KEY ([updated_by_user_id]) REFERENCES [dbo].[Users] ([id])
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Singleton table storing global LLM configuration for aggregation analysis', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'GlobalLlmConfiguration';

