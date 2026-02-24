CREATE TABLE [dbo].[Agents] (
    [id]                    INT             IDENTITY (1, 1) NOT NULL,
    [team_id]               INT             NOT NULL,
    [owner_user_id]         INT             NOT NULL,
    [name]                  NVARCHAR (255)  NOT NULL,
    [description]           NVARCHAR (1000) NULL,
    [visibility]            NVARCHAR (50)   CONSTRAINT [DF_Agents_Visibility] DEFAULT ('Private') NOT NULL,
    [version]               NVARCHAR (50)   NULL,
    [created_at]            DATETIME2 (7)   DEFAULT (getdate()) NULL,
    [updated_at]            DATETIME2 (7)   DEFAULT (getdate()) NULL,
    [is_deleted]            BIT             DEFAULT ((0)) NOT NULL,
    [deleted_at]            DATETIME2 (7)   NULL,
    [endpoint_url]          NVARCHAR (2000) NOT NULL,
    [http_method]           NVARCHAR (20)   DEFAULT ('GET') NULL,
    [auth_type]             NVARCHAR (50)   DEFAULT ('None') NULL,
    [username]              NVARCHAR (255)  NULL,
    [password]              VARBINARY (MAX) NULL,
    [api_token]             VARBINARY (MAX) NULL,
    [request_body_template] NVARCHAR (MAX)  NULL,
    [agent_instructions]    NVARCHAR (MAX)  NULL,
    [response_mapping]      NVARCHAR (MAX)  NULL,
    [login_endpoint_url]    NVARCHAR (2000) NULL,
    [llm_model_id]          INT             NULL,
    [encrypted_llm_api_key] VARBINARY (MAX) NULL,
    PRIMARY KEY CLUSTERED ([id] ASC),
    CONSTRAINT [CK_Agents_AuthType] CHECK ([auth_type]='UsernamePassword' OR [auth_type]='ApiToken' OR [auth_type]='BasicAuth' OR [auth_type]='None'),
    CONSTRAINT [CK_Agents_HttpMethod] CHECK ([http_method]='DELETE' OR [http_method]='PATCH' OR [http_method]='PUT' OR [http_method]='POST' OR [http_method]='GET'),
    CONSTRAINT [CK_Agents_Visibility] CHECK ([visibility]='Team' OR [visibility]='Private'),
    FOREIGN KEY ([owner_user_id]) REFERENCES [dbo].[Users] ([id]),
    FOREIGN KEY ([team_id]) REFERENCES [dbo].[Teams] ([id]),
    CONSTRAINT [FK_Agents_LlmModel] FOREIGN KEY ([llm_model_id]) REFERENCES [dbo].[LlmModels] ([id])
);




GO
CREATE NONCLUSTERED INDEX [IX_Agents_Team_Visibility]
    ON [dbo].[Agents]([team_id] ASC, [visibility] ASC) WHERE ([is_deleted]=(0));


GO
CREATE NONCLUSTERED INDEX [IX_Agents_LlmModelId]
    ON [dbo].[Agents]([llm_model_id] ASC) WHERE ([is_deleted]=(0));


GO
CREATE NONCLUSTERED INDEX [IX_Agents_EndpointUrl]
    ON [dbo].[Agents]([endpoint_url] ASC) WHERE ([is_deleted]=(0));


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'For auth_type=UsernamePassword: endpoint to POST username/password to obtain auth token', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Agents', @level2type = N'COLUMN', @level2name = N'login_endpoint_url';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Per-agent LLM model selection (nullable, can fall back to global)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Agents', @level2type = N'COLUMN', @level2name = N'llm_model_id';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'AES-256 encrypted API key for per-agent LLM access', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Agents', @level2type = N'COLUMN', @level2name = N'encrypted_llm_api_key';

