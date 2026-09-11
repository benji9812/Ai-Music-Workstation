using AiMusicWorkstation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiMusicWorkstation.Infrastructure.Migrations
{
    [DbContext(typeof(AiMusicWorkstationDbContext))]
    [Migration("20260911120000_AddImportJobId")]
    public partial class AddImportJobId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "import_job_id",
                table: "songs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_songs_import_job_id",
                table: "songs",
                column: "import_job_id",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_songs_import_job_id",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "import_job_id",
                table: "songs");
        }
    }
}
