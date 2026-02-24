CREATE TABLE [dbo].[TeamSecrets] (
    [team_id]         INT             NOT NULL,
    [key_name]        NVARCHAR (100)  NOT NULL,
    [encrypted_value] VARBINARY (MAX) NOT NULL,
    [created_at]      DATETIME2 (7)   DEFAULT (getdate()) NULL,
    PRIMARY KEY CLUSTERED ([team_id] ASC, [key_name] ASC),
    FOREIGN KEY ([team_id]) REFERENCES [dbo].[Teams] ([id])
);

