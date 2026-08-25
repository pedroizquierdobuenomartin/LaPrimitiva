using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaPrimitiva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWinningDraws : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'[DrawRecords]', N'Acumulado') IS NULL
                    ALTER TABLE [DrawRecords] ADD [Acumulado] decimal(12,2) NOT NULL CONSTRAINT [DF_DrawRecords_Acumulado] DEFAULT 0 WITH VALUES;
                IF COL_LENGTH(N'[DrawRecords]', N'CosteAuto') IS NULL
                    ALTER TABLE [DrawRecords] ADD [CosteAuto] decimal(10,2) NOT NULL CONSTRAINT [DF_DrawRecords_CosteAuto] DEFAULT 0 WITH VALUES;
                IF COL_LENGTH(N'[DrawRecords]', N'CosteFija') IS NULL
                    ALTER TABLE [DrawRecords] ADD [CosteFija] decimal(10,2) NOT NULL CONSTRAINT [DF_DrawRecords_CosteFija] DEFAULT 0 WITH VALUES;
                IF COL_LENGTH(N'[DrawRecords]', N'CosteJokerAuto') IS NULL
                    ALTER TABLE [DrawRecords] ADD [CosteJokerAuto] decimal(10,2) NOT NULL CONSTRAINT [DF_DrawRecords_CosteJokerAuto] DEFAULT 0 WITH VALUES;
                IF COL_LENGTH(N'[DrawRecords]', N'CosteJokerFija') IS NULL
                    ALTER TABLE [DrawRecords] ADD [CosteJokerFija] decimal(10,2) NOT NULL CONSTRAINT [DF_DrawRecords_CosteJokerFija] DEFAULT 0 WITH VALUES;
                IF COL_LENGTH(N'[DrawRecords]', N'Neto') IS NULL
                    ALTER TABLE [DrawRecords] ADD [Neto] decimal(10,2) NOT NULL CONSTRAINT [DF_DrawRecords_Neto] DEFAULT 0 WITH VALUES;
                IF COL_LENGTH(N'[DrawRecords]', N'TotalCoste') IS NULL
                    ALTER TABLE [DrawRecords] ADD [TotalCoste] decimal(10,2) NOT NULL CONSTRAINT [DF_DrawRecords_TotalCoste] DEFAULT 0 WITH VALUES;
                IF COL_LENGTH(N'[DrawRecords]', N'TotalPremios') IS NULL
                    ALTER TABLE [DrawRecords] ADD [TotalPremios] decimal(10,2) NOT NULL CONSTRAINT [DF_DrawRecords_TotalPremios] DEFAULT 0 WITH VALUES;

                IF OBJECT_ID(N'[WinningDraws]', N'U') IS NULL
                BEGIN
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
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[WinningDraws]') AND [name] = N'IX_WinningDraws_DrawDate')
                    CREATE UNIQUE INDEX [IX_WinningDraws_DrawDate] ON [WinningDraws] ([DrawDate]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WinningDraws");

            migrationBuilder.DropColumn(
                name: "Acumulado",
                table: "DrawRecords");

            migrationBuilder.DropColumn(
                name: "CosteAuto",
                table: "DrawRecords");

            migrationBuilder.DropColumn(
                name: "CosteFija",
                table: "DrawRecords");

            migrationBuilder.DropColumn(
                name: "CosteJokerAuto",
                table: "DrawRecords");

            migrationBuilder.DropColumn(
                name: "CosteJokerFija",
                table: "DrawRecords");

            migrationBuilder.DropColumn(
                name: "Neto",
                table: "DrawRecords");

            migrationBuilder.DropColumn(
                name: "TotalCoste",
                table: "DrawRecords");

            migrationBuilder.DropColumn(
                name: "TotalPremios",
                table: "DrawRecords");
        }
    }
}
