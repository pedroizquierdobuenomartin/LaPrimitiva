using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaPrimitiva.Infrastructure.Migrations
{
    [DbContext(typeof(PrimitivaDbContext))]
    [Migration("20260820160000_ValidatePlans")]
    public partial class ValidatePlans : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [Plans]
                SET [JokerCostPerBet] = 0
                WHERE [EnableJoker] = 0 AND [JokerCostPerBet] <> 0;

                IF EXISTS (
                    SELECT 1
                    FROM [Plans] firstPlan
                    INNER JOIN [Plans] secondPlan ON firstPlan.[Id] < secondPlan.[Id]
                    WHERE (firstPlan.[EffectiveTo] IS NULL OR secondPlan.[EffectiveFrom] <= firstPlan.[EffectiveTo])
                      AND (secondPlan.[EffectiveTo] IS NULL OR secondPlan.[EffectiveTo] >= firstPlan.[EffectiveFrom]))
                    THROW 51000, 'No se pueden activar las restricciones: existen planes solapados.', 1;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plans_EffectivePeriod",
                table: "Plans",
                sql: "[EffectiveTo] IS NULL OR [EffectiveFrom] <= [EffectiveTo]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plans_Name",
                table: "Plans",
                sql: "LEN(LTRIM(RTRIM([Name]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plans_NonNegativeValues",
                table: "Plans",
                sql: "[WeeksToTrackDefault] >= 0 AND [CostPerBet] >= 0 AND [JokerCostPerBet] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plans_BetsPerDraw",
                table: "Plans",
                sql: "[BetsPerDraw] BETWEEN 1 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plans_DisabledJokerCost",
                table: "Plans",
                sql: "[EnableJoker] = 1 OR [JokerCostPerBet] = 0");

            migrationBuilder.Sql(@"
                CREATE OR ALTER TRIGGER [TR_Plans_PreventOverlap]
                ON [Plans]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted candidate
                        INNER JOIN [Plans] existing ON existing.[Id] <> candidate.[Id]
                        WHERE (candidate.[EffectiveTo] IS NULL OR existing.[EffectiveFrom] <= candidate.[EffectiveTo])
                          AND (existing.[EffectiveTo] IS NULL OR existing.[EffectiveTo] >= candidate.[EffectiveFrom]))
                        THROW 51001, 'El periodo del plan se solapa con otro plan existente.', 1;
                END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_Plans_PreventOverlap];");
            migrationBuilder.DropCheckConstraint(name: "CK_Plans_DisabledJokerCost", table: "Plans");
            migrationBuilder.DropCheckConstraint(name: "CK_Plans_BetsPerDraw", table: "Plans");
            migrationBuilder.DropCheckConstraint(name: "CK_Plans_NonNegativeValues", table: "Plans");
            migrationBuilder.DropCheckConstraint(name: "CK_Plans_Name", table: "Plans");
            migrationBuilder.DropCheckConstraint(name: "CK_Plans_EffectivePeriod", table: "Plans");
        }
    }
}
