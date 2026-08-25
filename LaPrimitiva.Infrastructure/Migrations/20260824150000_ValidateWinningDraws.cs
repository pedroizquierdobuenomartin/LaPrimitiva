using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaPrimitiva.Infrastructure.Migrations
{
    [DbContext(typeof(PrimitivaDbContext))]
    [Migration("20260824150000_ValidateWinningDraws")]
    public partial class ValidateWinningDraws : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [WinningDraws]
                SET [Joker] = NULL
                WHERE LTRIM(RTRIM([Joker])) = '';

                UPDATE [WinningDraws]
                SET [Joker] = RIGHT(REPLICATE('0', 7) + [Joker], 7)
                WHERE [Joker] NOT LIKE '%[^0-9]%'
                  AND DATALENGTH([Joker]) BETWEEN 2 AND 12;

                IF EXISTS (
                    SELECT 1 FROM [WinningDraws]
                    WHERE [Number1] NOT BETWEEN 1 AND 49 OR [Number2] NOT BETWEEN 1 AND 49
                       OR [Number3] NOT BETWEEN 1 AND 49 OR [Number4] NOT BETWEEN 1 AND 49
                       OR [Number5] NOT BETWEEN 1 AND 49 OR [Number6] NOT BETWEEN 1 AND 49
                       OR [Number1] IN ([Number2], [Number3], [Number4], [Number5], [Number6])
                       OR [Number2] IN ([Number3], [Number4], [Number5], [Number6])
                       OR [Number3] IN ([Number4], [Number5], [Number6])
                       OR [Number4] IN ([Number5], [Number6]) OR [Number5] = [Number6]
                       OR [Complementario] NOT BETWEEN 1 AND 49
                       OR [Complementario] IN ([Number1], [Number2], [Number3], [Number4], [Number5], [Number6])
                       OR [Reintegro] NOT BETWEEN 0 AND 9
                       OR ([Joker] IS NOT NULL AND (DATALENGTH([Joker]) <> 14 OR [Joker] LIKE '%[^0-9]%')))
                    THROW 51002, 'No se pueden activar las restricciones: existen sorteos históricos inválidos.', 1;");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE [object_id] = OBJECT_ID(N'[WinningDraws]')
                      AND [name] = N'Joker'
                      AND [max_length] <> 14)
                    ALTER TABLE [WinningDraws] ALTER COLUMN [Joker] nvarchar(7) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'[WinningDraws]') AND [name] = N'CK_WinningDraws_MainNumbersRange')
                    ALTER TABLE [WinningDraws] WITH CHECK ADD CONSTRAINT [CK_WinningDraws_MainNumbersRange] CHECK ([Number1] BETWEEN 1 AND 49 AND [Number2] BETWEEN 1 AND 49 AND [Number3] BETWEEN 1 AND 49 AND [Number4] BETWEEN 1 AND 49 AND [Number5] BETWEEN 1 AND 49 AND [Number6] BETWEEN 1 AND 49);
                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'[WinningDraws]') AND [name] = N'CK_WinningDraws_MainNumbersDistinct')
                    ALTER TABLE [WinningDraws] WITH CHECK ADD CONSTRAINT [CK_WinningDraws_MainNumbersDistinct] CHECK ([Number1] NOT IN ([Number2], [Number3], [Number4], [Number5], [Number6]) AND [Number2] NOT IN ([Number3], [Number4], [Number5], [Number6]) AND [Number3] NOT IN ([Number4], [Number5], [Number6]) AND [Number4] NOT IN ([Number5], [Number6]) AND [Number5] <> [Number6]);
                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'[WinningDraws]') AND [name] = N'CK_WinningDraws_Complementario')
                    ALTER TABLE [WinningDraws] WITH CHECK ADD CONSTRAINT [CK_WinningDraws_Complementario] CHECK ([Complementario] BETWEEN 1 AND 49 AND [Complementario] NOT IN ([Number1], [Number2], [Number3], [Number4], [Number5], [Number6]));
                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'[WinningDraws]') AND [name] = N'CK_WinningDraws_Reintegro')
                    ALTER TABLE [WinningDraws] WITH CHECK ADD CONSTRAINT [CK_WinningDraws_Reintegro] CHECK ([Reintegro] BETWEEN 0 AND 9);
                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'[WinningDraws]') AND [name] = N'CK_WinningDraws_Joker')
                    ALTER TABLE [WinningDraws] WITH CHECK ADD CONSTRAINT [CK_WinningDraws_Joker] CHECK ([Joker] IS NULL OR (DATALENGTH([Joker]) = 14 AND [Joker] NOT LIKE '%[^0-9]%'));");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "CK_WinningDraws_Joker", table: "WinningDraws");
            migrationBuilder.DropCheckConstraint(name: "CK_WinningDraws_Reintegro", table: "WinningDraws");
            migrationBuilder.DropCheckConstraint(name: "CK_WinningDraws_Complementario", table: "WinningDraws");
            migrationBuilder.DropCheckConstraint(name: "CK_WinningDraws_MainNumbersDistinct", table: "WinningDraws");
            migrationBuilder.DropCheckConstraint(name: "CK_WinningDraws_MainNumbersRange", table: "WinningDraws");

            migrationBuilder.AlterColumn<string>(
                name: "Joker",
                table: "WinningDraws",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7,
                oldNullable: true);
        }
    }
}
