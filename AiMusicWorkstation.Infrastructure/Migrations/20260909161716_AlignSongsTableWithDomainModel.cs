using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiMusicWorkstation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignSongsTableWithDomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE public.songs
                    ALTER COLUMN id DROP DEFAULT,
                    ALTER COLUMN id TYPE text USING id::text;

                UPDATE public.songs
                SET
                    artist = COALESCE(artist, 'Unknown Artist'),
                    bpm = COALESCE(bpm, 0),
                    "key" = COALESCE("key", ''),
                    time_signature = CASE
                        WHEN btrim(COALESCE(time_signature, '')) ~ '^[1-9][0-9]*(/[1-9][0-9]*)?$'
                            THEN split_part(btrim(time_signature), '/', 1)
                        ELSE '4'
                    END,
                    duration = COALESCE(duration, 0);

                ALTER TABLE public.songs
                    ALTER COLUMN artist SET NOT NULL,
                    ALTER COLUMN bpm TYPE double precision USING bpm::double precision,
                    ALTER COLUMN bpm SET NOT NULL,
                    ALTER COLUMN "key" SET NOT NULL,
                    ALTER COLUMN time_signature TYPE integer USING time_signature::integer,
                    ALTER COLUMN time_signature SET NOT NULL,
                    ALTER COLUMN duration TYPE interval USING make_interval(secs => duration),
                    ALTER COLUMN duration SET NOT NULL;

                ALTER TABLE public.songs
                    ALTER COLUMN "BpmSource" TYPE integer USING
                        CASE lower(btrim(COALESCE("BpmSource", '')))
                            WHEN 'spotify' THEN 1
                            WHEN 'analysis' THEN 2
                            WHEN '1' THEN 1
                            WHEN '2' THEN 2
                            ELSE 0
                        END,
                    ALTER COLUMN "BpmSource" SET NOT NULL,
                    ALTER COLUMN "KeySource" TYPE integer USING
                        CASE lower(btrim(COALESCE("KeySource", '')))
                            WHEN 'spotify' THEN 1
                            WHEN 'analysis' THEN 2
                            WHEN '1' THEN 1
                            WHEN '2' THEN 2
                            ELSE 0
                        END,
                    ALTER COLUMN "KeySource" SET NOT NULL,
                    ALTER COLUMN "TimeSignatureSource" TYPE integer USING
                        CASE lower(btrim(COALESCE("TimeSignatureSource", '')))
                            WHEN 'spotify' THEN 1
                            WHEN 'analysis' THEN 2
                            WHEN '1' THEN 1
                            WHEN '2' THEN 2
                            ELSE 0
                        END,
                    ALTER COLUMN "TimeSignatureSource" SET NOT NULL,
                    ALTER COLUMN "StructureSource" TYPE integer USING
                        CASE lower(btrim(COALESCE("StructureSource", '')))
                            WHEN 'spotify' THEN 1
                            WHEN 'analysis' THEN 2
                            WHEN '1' THEN 1
                            WHEN '2' THEN 2
                            ELSE 0
                        END,
                    ALTER COLUMN "StructureSource" SET NOT NULL;

                ALTER TABLE public.songs
                    ADD COLUMN date_added timestamp with time zone NOT NULL DEFAULT now(),
                    ADD COLUMN genre text NOT NULL DEFAULT 'Uncategorized',
                    ADD COLUMN group_name text NOT NULL DEFAULT 'General',
                    ADD COLUMN is_official_data boolean NOT NULL DEFAULT false,
                    ADD COLUMN original_path text NOT NULL DEFAULT '',
                    ADD COLUMN sections text NOT NULL DEFAULT '[]',
                    ADD COLUMN spotify_id text NOT NULL DEFAULT '',
                    ADD COLUMN stems_path text NOT NULL DEFAULT '';

                ALTER TABLE public.songs
                    ALTER COLUMN date_added DROP DEFAULT,
                    ALTER COLUMN genre DROP DEFAULT,
                    ALTER COLUMN group_name DROP DEFAULT,
                    ALTER COLUMN is_official_data DROP DEFAULT,
                    ALTER COLUMN original_path DROP DEFAULT,
                    ALTER COLUMN sections DROP DEFAULT,
                    ALTER COLUMN spotify_id DROP DEFAULT,
                    ALTER COLUMN stems_path DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE public.songs
                    DROP COLUMN date_added,
                    DROP COLUMN genre,
                    DROP COLUMN group_name,
                    DROP COLUMN is_official_data,
                    DROP COLUMN original_path,
                    DROP COLUMN sections,
                    DROP COLUMN spotify_id,
                    DROP COLUMN stems_path;

                ALTER TABLE public.songs
                    ALTER COLUMN artist DROP NOT NULL,
                    ALTER COLUMN bpm DROP NOT NULL,
                    ALTER COLUMN bpm TYPE integer USING round(bpm)::integer,
                    ALTER COLUMN "key" DROP NOT NULL,
                    ALTER COLUMN time_signature DROP NOT NULL,
                    ALTER COLUMN time_signature TYPE text USING time_signature::text,
                    ALTER COLUMN duration DROP NOT NULL,
                    ALTER COLUMN duration TYPE integer USING round(extract(epoch FROM duration))::integer;

                ALTER TABLE public.songs
                    ALTER COLUMN "BpmSource" DROP NOT NULL,
                    ALTER COLUMN "BpmSource" TYPE text USING
                        CASE "BpmSource"
                            WHEN 1 THEN 'Spotify'
                            WHEN 2 THEN 'Analysis'
                            ELSE 'Unknown'
                        END,
                    ALTER COLUMN "KeySource" DROP NOT NULL,
                    ALTER COLUMN "KeySource" TYPE text USING
                        CASE "KeySource"
                            WHEN 1 THEN 'Spotify'
                            WHEN 2 THEN 'Analysis'
                            ELSE 'Unknown'
                        END,
                    ALTER COLUMN "TimeSignatureSource" DROP NOT NULL,
                    ALTER COLUMN "TimeSignatureSource" TYPE text USING
                        CASE "TimeSignatureSource"
                            WHEN 1 THEN 'Spotify'
                            WHEN 2 THEN 'Analysis'
                            ELSE 'Unknown'
                        END,
                    ALTER COLUMN "StructureSource" DROP NOT NULL,
                    ALTER COLUMN "StructureSource" TYPE text USING
                        CASE "StructureSource"
                            WHEN 1 THEN 'Spotify'
                            WHEN 2 THEN 'Analysis'
                            ELSE 'Unknown'
                        END;

                ALTER TABLE public.songs
                    ALTER COLUMN id TYPE uuid USING id::uuid,
                    ALTER COLUMN id SET DEFAULT gen_random_uuid();
                """);
        }
    }
}
