using System.Text.Json;
using AiMusicWorkstation.Domain.Entities;

namespace AiMusicWorkstation.Api.Models;

public sealed record SavedSongProjectDto(
    string Id,
    string Title,
    string Artist,
    string Genre,
    DateTime DateAdded,
    double Bpm,
    string Key,
    int TimeSignature,
    double DurationSeconds,
    string StemsPath,
    string OriginalPath,
    IReadOnlyList<SavedLyricDto> Lyrics,
    IReadOnlyList<SavedSectionDto> Sections,
    IReadOnlyList<SavedChordDto> Chords,
    string? BpmSource,
    string? KeySource,
    string? TimeSignatureSource,
    string? SectionsSource,
    string? LyricsSource,
    string? ChordsSource,
    Guid? GroupId,
    SavedSongGroupDto? Group);

public sealed record SavedLyricDto(double Start, double End, string Text);
public sealed record SavedSectionDto(string Label, double Start, double End);
public sealed record SavedChordDto(double Time, string Chord);
public sealed record SavedSongGroupDto(Guid Id, string Name, DateTime CreatedAt);

public static class SavedSongProjectMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static SavedSongProjectDto Map(SongProject project, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(logger);

        return new SavedSongProjectDto(
            project.Id,
            project.Title,
            project.Artist,
            project.Genre,
            project.DateAdded,
            project.Bpm,
            project.Key,
            project.TimeSignature,
            project.Duration.TotalSeconds,
            project.StemsPath,
            project.OriginalPath,
            ParseArray<SavedLyricDto>(project.Lyrics, project.Id, nameof(project.Lyrics), logger),
            ParseArray<SavedSectionDto>(project.Sections, project.Id, nameof(project.Sections), logger),
            ParseArray<SavedChordDto>(project.Chords, project.Id, nameof(project.Chords), logger),
            MapSource(project.BpmSource),
            MapSource(project.KeySource),
            MapSource(project.TimeSigSource),
            MapSource(project.SectionsSource),
            MapSource(project.LyricsSource),
            MapSource(project.ChordsSource),
            project.GroupId,
            project.Group is null
                ? null
                : new SavedSongGroupDto(project.Group.Id, project.Group.Name, project.Group.CreatedAt));
    }

    private static IReadOnlyList<T> ParseArray<T>(
        string json,
        string projectId,
        string fieldName,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            logger.LogWarning(
                "Saved project {ProjectId} has malformed JSON in {FieldName}; returning an empty array.",
                projectId,
                fieldName);
            return [];
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
            if (values is not null)
            {
                return values;
            }

            logger.LogWarning(
                "Saved project {ProjectId} has non-array JSON in {FieldName}; returning an empty array.",
                projectId,
                fieldName);
            return [];
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Saved project {ProjectId} has malformed JSON in {FieldName}; returning an empty array.",
                projectId,
                fieldName);
            return [];
        }
    }

    private static string? MapSource(DataSource source) => source switch
    {
        DataSource.Spotify => "spotify",
        DataSource.Analysis => "analysis",
        _ => null
    };
}
