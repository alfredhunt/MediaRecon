using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexBytez.MediaRecon.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Directories_DirectoryId",
                table: "Files");

            migrationBuilder.DropTable(
                name: "Directories");

            migrationBuilder.DropIndex(
                name: "IX_Files_DirectoryId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "DirectoryId",
                table: "Files");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DirectoryId",
                table: "Files",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Directories",
                columns: table => new
                {
                    DirectoryId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", nullable: false),
                    LastAccessTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastWriteTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Directories", x => x.DirectoryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Files_DirectoryId",
                table: "Files",
                column: "DirectoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Directories_DirectoryId",
                table: "Files",
                column: "DirectoryId",
                principalTable: "Directories",
                principalColumn: "DirectoryId");
        }
    }
}
