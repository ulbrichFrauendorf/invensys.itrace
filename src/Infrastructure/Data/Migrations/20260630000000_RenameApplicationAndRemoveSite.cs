using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    public partial class RenameApplicationAndRemoveSite : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_Name_Environment_SiteName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "SiteName",
                table: "Applications");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Name_Environment",
                table: "Applications",
                columns: new[] { "Name", "Environment" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_Name_Environment",
                table: "Applications");

            migrationBuilder.AddColumn<string>(
                name: "SiteName",
                table: "Applications",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "Default");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Name_Environment_SiteName",
                table: "Applications",
                columns: new[] { "Name", "Environment", "SiteName" },
                unique: true);
        }
    }
}
