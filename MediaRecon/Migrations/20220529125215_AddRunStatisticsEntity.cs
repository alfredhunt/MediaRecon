using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexBytez.MediaRecon.Migrations
{
    public partial class AddRunStatisticsEntity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunStatistics",
                columns: table => new
                {
                    RunStatisticsId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompletedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NumberOfFilesAnalayzed = table.Column<long>(type: "INTEGER", nullable: false),
                    AmountOfDataAnalyzed = table.Column<long>(type: "INTEGER", nullable: false),
                    DuplicateFilesFound = table.Column<long>(type: "INTEGER", nullable: false),
                    DuplicateFilesRemoved = table.Column<long>(type: "INTEGER", nullable: false),
                    AmountOfDuplicateDataRemoved = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunStatistics", x => x.RunStatisticsId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunStatistics");
        }
    }
}
