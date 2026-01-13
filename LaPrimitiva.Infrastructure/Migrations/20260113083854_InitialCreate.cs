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
            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WeeksToTrackDefault = table.Column<int>(type: "int", nullable: false),
                    CostPerBet = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    BetsPerDraw = table.Column<int>(type: "int", nullable: false),
                    EnableJoker = table.Column<bool>(type: "bit", nullable: false),
                    JokerCostPerBet = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    FixedCombinationLabel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrawRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DrawType = table.Column<byte>(type: "tinyint", nullable: false),
                    DrawDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WeekNumber = table.Column<int>(type: "int", nullable: false),
                    Played = table.Column<bool>(type: "bit", nullable: false),
                    FixedPrize = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AutoPrize = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    JokerFixedPrize = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    JokerAutoPrize = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrawRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrawRecords_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DrawRecords_DrawDate",
                table: "DrawRecords",
                column: "DrawDate");

            migrationBuilder.CreateIndex(
                name: "IX_DrawRecords_PlanId_DrawDate_DrawType",
                table: "DrawRecords",
                columns: new[] { "PlanId", "DrawDate", "DrawType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrawRecords_WeekNumber",
                table: "DrawRecords",
                column: "WeekNumber");
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
