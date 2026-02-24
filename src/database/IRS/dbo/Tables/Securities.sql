CREATE TABLE [dbo].[Securities] (
    [figi]           NVARCHAR (50)  NOT NULL,
    [ticker]         NVARCHAR (50)  NULL,
    [name]           NVARCHAR (255) NULL,
    [market_sector]         NVARCHAR (100) NULL,
    [security_type]  NVARCHAR (20)  NULL,
    [exchange_code]    NVARCHAR (50)  NULL,
    [last_synced_at] DATETIME2 (7)  NULL,
    [mic_code] NVARCHAR(50) NULL, 
    [share_class_figi] NVARCHAR(50) NULL, 
    [composite_figi] NVARCHAR(50) NULL, 
    [security_type2] NVARCHAR(50) NULL, 
    [security_description] NVARCHAR(100) NULL, 
    PRIMARY KEY CLUSTERED ([figi] ASC),
    CONSTRAINT [CK_Securities_SecurityType] CHECK ([security_type]='Bond' OR [security_type]='Equity' OR [security_type]='Corporate' OR [security_type]='Sovereign')
);

