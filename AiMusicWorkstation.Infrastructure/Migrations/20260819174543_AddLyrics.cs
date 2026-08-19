using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiMusicWorkstation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLyrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LyricsSource",
                table: "songs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "lyrics",
                table: "songs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LyricsSource",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "lyrics",
                table: "songs");
        }
    }
}
