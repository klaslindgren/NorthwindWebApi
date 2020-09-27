using Microsoft.EntityFrameworkCore.Migrations;

namespace NorthwindWebApi.Migrations
{
    public partial class UpdUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationToken",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "JwtToken",
                table: "AspNetUsers",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JwtToken",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "VerificationToken",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
