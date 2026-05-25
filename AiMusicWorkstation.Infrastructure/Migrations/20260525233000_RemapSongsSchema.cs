using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiMusicWorkstation.Infrastructure.Migrations
{
    public partial class RemapSongsSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS song_groups (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    user_id UUID REFERENCES auth.users(id),
    created_at TIMESTAMPTZ DEFAULT now()
);
");

            migrationBuilder.Sql(@"
ALTER TABLE songs
ADD CONSTRAINT songs_group_id_fkey
FOREIGN KEY (group_id) REFERENCES song_groups(id) ON DELETE SET NULL;
");

            migrationBuilder.Sql(@"
INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
VALUES ('99999999999999_DropLegacyTables', '10.0.4')
ON CONFLICT DO NOTHING;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE songs
DROP CONSTRAINT IF EXISTS songs_group_id_fkey;
");

            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS song_groups;
");

            migrationBuilder.Sql(@"
DELETE FROM ""__EFMigrationsHistory""
WHERE ""MigrationId"" = '99999999999999_DropLegacyTables';
");
        }
    }
}
