using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiMusicWorkstation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BpmSource",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TimeSigSource",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KeySource",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BpmSource",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TimeSigSource",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "KeySource",
                table: "Projects");
        }
    }
}
