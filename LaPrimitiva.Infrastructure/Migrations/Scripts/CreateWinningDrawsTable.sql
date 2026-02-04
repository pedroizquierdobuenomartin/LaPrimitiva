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
GO

CREATE UNIQUE INDEX [IX_WinningDraws_DrawDate] ON [WinningDraws] ([DrawDate]);
GO
