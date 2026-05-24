using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiMusicWorkstation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sections",
                table: "Projects",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "SectionsSource",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sections",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SectionsSource",
                table: "Projects");
        }
    }
}
