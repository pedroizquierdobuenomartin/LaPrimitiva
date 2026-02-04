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
            migrationBuilder.AddColumn<decimal>(
                name: "Acumulado",
                table: "DrawRecords",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CosteAuto",
                table: "DrawRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CosteFija",
                table: "DrawRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CosteJokerAuto",
                table: "DrawRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CosteJokerFija",
                table: "DrawRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Neto",
                table: "DrawRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCoste",
                table: "DrawRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPremios",
                table: "DrawRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "WinningDraws",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DrawDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Number1 = table.Column<int>(type: "int", nullable: false),
                    Number2 = table.Column<int>(type: "int", nullable: false),
                    Number3 = table.Column<int>(type: "int", nullable: false),
                    Number4 = table.Column<int>(type: "int", nullable: false),
                    Number5 = table.Column<int>(type: "int", nullable: false),
                    Number6 = table.Column<int>(type: "int", nullable: false),
                    Complementario = table.Column<int>(type: "int", nullable: false),
                    Reintegro = table.Column<int>(type: "int", nullable: false),
                    Joker = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WinningDraws", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WinningDraws_DrawDate",
                table: "WinningDraws",
                column: "DrawDate",
                unique: true);
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
