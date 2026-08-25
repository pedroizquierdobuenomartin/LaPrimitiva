using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaPrimitiva.Infrastructure.Migrations
{
    [DbContext(typeof(PrimitivaDbContext))]
    [Migration("20260825120000_AddConcurrencyTokens")]
    public partial class AddConcurrencyTokens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'[WinningDraws]', N'RowVersion') IS NULL
                    ALTER TABLE [WinningDraws] ADD [RowVersion] rowversion NOT NULL;

                IF COL_LENGTH(N'[Plans]', N'RowVersion') IS NULL
                    ALTER TABLE [Plans] ADD [RowVersion] rowversion NOT NULL;

                IF COL_LENGTH(N'[DrawRecords]', N'RowVersion') IS NULL
                    ALTER TABLE [DrawRecords] ADD [RowVersion] rowversion NOT NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'[WinningDraws]', N'RowVersion') IS NOT NULL
                    ALTER TABLE [WinningDraws] DROP COLUMN [RowVersion];

                IF COL_LENGTH(N'[Plans]', N'RowVersion') IS NOT NULL
                    ALTER TABLE [Plans] DROP COLUMN [RowVersion];

                IF COL_LENGTH(N'[DrawRecords]', N'RowVersion') IS NOT NULL
                    ALTER TABLE [DrawRecords] DROP COLUMN [RowVersion];");
        }
    }
}
