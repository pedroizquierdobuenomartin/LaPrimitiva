BEGIN TRANSACTION;
ALTER TABLE [DrawRecords] ADD [Acumulado] decimal(12,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [DrawRecords] ADD [CosteAuto] decimal(10,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [DrawRecords] ADD [CosteFija] decimal(10,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [DrawRecords] ADD [CosteJokerAuto] decimal(10,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [DrawRecords] ADD [CosteJokerFija] decimal(10,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [DrawRecords] ADD [Neto] decimal(10,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [DrawRecords] ADD [TotalCoste] decimal(10,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [DrawRecords] ADD [TotalPremios] decimal(10,2) NOT NULL DEFAULT 0.0;

CREATE TABLE [WinningDraws] (
    [Id] uniqueidentifier NOT NULL,
    [DrawDate] datetime2 NOT NULL,
    [Number1] int NOT NULL,
    [Number2] int NOT NULL,
    [Number3] int NOT NULL,
    [Number4] int NOT NULL,
    [Number5] int NOT NULL,
    [Number6] int NOT NULL,
    [Complementario] int NOT NULL,
    [Reintegro] int NOT NULL,
    [Joker] nvarchar(10) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WinningDraws] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_WinningDraws_DrawDate] ON [WinningDraws] ([DrawDate]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260204135951_AddWinningDraws', N'10.0.1');

COMMIT;
GO

