using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaPrimitiva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The application historically created these tables at startup without
            // recording migration history. Conditional creation lets EF adopt that
            // legacy schema without dropping or recreating its data.
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Plans]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Plans] (
                        [Id] uniqueidentifier NOT NULL,
                        [Name] nvarchar(max) NOT NULL,
                        [EffectiveFrom] datetime2 NOT NULL,
                        [EffectiveTo] datetime2 NULL,
                        [WeeksToTrackDefault] int NOT NULL,
                        [CostPerBet] decimal(10,2) NOT NULL,
                        [BetsPerDraw] int NOT NULL,
                        [EnableJoker] bit NOT NULL,
                        [JokerCostPerBet] decimal(10,2) NOT NULL,
                        [FixedCombinationLabel] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_Plans] PRIMARY KEY ([Id])
                    );
                END;

                IF OBJECT_ID(N'[DrawRecords]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [DrawRecords] (
                        [Id] uniqueidentifier NOT NULL,
                        [PlanId] uniqueidentifier NOT NULL,
                        [DrawType] tinyint NOT NULL,
                        [DrawDate] datetime2 NOT NULL,
                        [WeekNumber] int NOT NULL,
                        [Played] bit NOT NULL,
                        [FixedPrize] decimal(10,2) NOT NULL,
                        [AutoPrize] decimal(10,2) NOT NULL,
                        [JokerFixedPrize] decimal(10,2) NOT NULL,
                        [JokerAutoPrize] decimal(10,2) NOT NULL,
                        [Notes] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_DrawRecords] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_DrawRecords_Plans_PlanId]
                            FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([Id]) ON DELETE CASCADE
                    );
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[DrawRecords]') AND [name] = N'IX_DrawRecords_DrawDate')
                    CREATE INDEX [IX_DrawRecords_DrawDate] ON [DrawRecords] ([DrawDate]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[DrawRecords]') AND [name] = N'IX_DrawRecords_PlanId_DrawDate_DrawType')
                    CREATE UNIQUE INDEX [IX_DrawRecords_PlanId_DrawDate_DrawType] ON [DrawRecords] ([PlanId], [DrawDate], [DrawType]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[DrawRecords]') AND [name] = N'IX_DrawRecords_WeekNumber')
                    CREATE INDEX [IX_DrawRecords_WeekNumber] ON [DrawRecords] ([WeekNumber]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DrawRecords");

            migrationBuilder.DropTable(
                name: "Plans");
        }
    }
}
