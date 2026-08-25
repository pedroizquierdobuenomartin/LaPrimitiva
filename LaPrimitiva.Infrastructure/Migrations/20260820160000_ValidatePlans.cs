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

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'[Plans]') AND [name] = N'CK_Plans_EffectivePeriod')
                    ALTER TABLE [Plans] WITH CHECK ADD CONSTRAINT [CK_Plans_EffectivePeriod] CHECK ([EffectiveTo] IS NULL OR [EffectiveFrom] <= [EffectiveTo]);
                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'[Plans]') AND [name] = N'CK_Plans_Name')
                    ALTER TABLE [Plans] WITH CHECK ADD CONSTRAINT [CK_Plans_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0);
                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'[Plans]') AND [name] = N'CK_Plans_NonNegativeValues')
                    ALTER TABLE [Plans] WITH CHECK ADD CONSTRAINT [CK_Plans_NonNegativeValues] CHECK ([WeeksToTrackDefault] >= 0 AND [CostPerBet] >= 0 AND [JokerCostPerBet] >= 0);
                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'[Plans]') AND [name] = N'CK_Plans_BetsPerDraw')
                    ALTER TABLE [Plans] WITH CHECK ADD CONSTRAINT [CK_Plans_BetsPerDraw] CHECK ([BetsPerDraw] BETWEEN 1 AND 100);
                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'[Plans]') AND [name] = N'CK_Plans_DisabledJokerCost')
                    ALTER TABLE [Plans] WITH CHECK ADD CONSTRAINT [CK_Plans_DisabledJokerCost] CHECK ([EnableJoker] = 1 OR [JokerCostPerBet] = 0);");

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
