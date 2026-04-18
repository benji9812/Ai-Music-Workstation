using AiMusicWorkstation.Desktop.Models;
using AiMusicWorkstation.Shared.Models;
using System.Text.Json;
using System.Linq;

namespace AiMusicWorkstation.Desktop.Services;

public class AiAnalysisOrchestrator
{
    private readonly PythonBridge _pythonBridge;

    public AiAnalysisOrchestrator(PythonBridge pythonBridge)
    {
        _pythonBridge = pythonBridge ?? throw new ArgumentNullException(nameof(pythonBridge));
    }

    public async Task<AnalysisParseResult> RunAnalysisAsync(string filePath, bool useCloud = true)
    {
        string jsonResponse = await _pythonBridge.RunAnalysisAsync(filePath, useCloud: useCloud);
        return ParseResponse(jsonResponse);
    }

    public async Task<AnalysisResult?> TryReAnalyzeAsync(string filePath)
    {
        string jsonResponse = await _pythonBridge.ReAnalyzeAsync(filePath);
        return TryParseResult(jsonResponse);
    }

    public async Task<AnalysisParseResult> RunReAnalyzeAsync(string filePath)
    {
        string jsonResponse = await _pythonBridge.ReAnalyzeAsync(filePath);
        return ParseResponse(jsonResponse);
    }

    public async Task<StructureParseResult> GetStructureAsync(string artist, string title, double duration)
    {
        string jsonResponse = await _pythonBridge.GetStructureAsync(artist, title, duration);
        return ParseStructure(jsonResponse, duration);
    }

    public static AnalysisParseResult ParseResponse(string jsonResponse)
    {
        int jsonStartIndex = jsonResponse.IndexOf('{');
        if (jsonStartIndex == -1)
        {
            return new AnalysisParseResult(
                null,
                jsonResponse,
                new AnalysisParseError(
                    "Analysis Error.",
                    $"Python gav inget giltigt svar:\n{jsonResponse}"));
        }

        string cleanJson = jsonResponse.Substring(jsonStartIndex);
        AnalysisResult? analysisData;

        try
        {
            analysisData = JsonSerializer.Deserialize<AnalysisResult>(cleanJson);
        }
        catch (Exception ex)
        {
            return new AnalysisParseResult(
                null,
                jsonResponse,
                new AnalysisParseError(
                    "Data Format Error.",
                    $"Kunde inte läsa datan från Python.\nFel: {ex.Message}"));
        }

        if (analysisData == null || analysisData.Status != "success")
        {
            string errorMsg = analysisData?.Message ?? "Okänt fel";
            return new AnalysisParseResult(
                analysisData,
                jsonResponse,
                new AnalysisParseError(
                    "Analysis failed.",
                    $"AI-motorn rapporterade ett fel:\n\n{errorMsg}"));
        }

        return new AnalysisParseResult(analysisData, jsonResponse, null);
    }

    public static AnalysisResult? TryParseResult(string jsonResponse)
    {
        int jsonStartIndex = jsonResponse.IndexOf('{');
        if (jsonStartIndex == -1) return null;

        string cleanJson = jsonResponse.Substring(jsonStartIndex);
        try
        {
            var result = JsonSerializer.Deserialize<AnalysisResult>(cleanJson);
            return result != null && result.Status == "success" ? result : null;
        }
        catch
        {
            return null;
        }
    }

    public static StructureParseResult ParseStructure(string jsonResponse, double duration)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            if (doc.RootElement.GetProperty("status").GetString() != "success")
            {
                string msg = doc.RootElement.TryGetProperty("message", out var m)
                    ? m.GetString() ?? "Unknown error"
                    : "Unknown error";
                return new StructureParseResult(new List<SongSection>(), msg);
            }

            var sectionsEl = doc.RootElement.GetProperty("sections");
            var newSections = new List<SongSection>();

            foreach (var s in sectionsEl.EnumerateArray())
            {
                string label = s.GetProperty("label").GetString() ?? "Section";
                double start = s.GetProperty("start").GetDouble();
                double end = s.GetProperty("end").GetDouble();

                if (start < 0 || start >= duration) continue;
                end = Math.Min(end, duration);

                newSections.Add(new SongSection
                {
                    Label = label,
                    StartTime = start,
                    EndTime = end,
                    Color = SectionColors.Get(label)
                });
            }

            newSections = newSections.OrderBy(s => s.StartTime).ToList();
            for (int i = 0; i < newSections.Count - 1; i++)
                newSections[i].EndTime = newSections[i + 1].StartTime;
            if (newSections.Any())
                newSections[^1].EndTime = duration;

            return new StructureParseResult(newSections, null);
        }
        catch (Exception ex)
        {
            return new StructureParseResult(new List<SongSection>(), ex.Message);
        }
    }
}

public record AnalysisParseResult(AnalysisResult? Result, string RawResponse, AnalysisParseError? Error);

public record AnalysisParseError(string StatusLabel, string MessageBoxText);

public record StructureParseResult(IReadOnlyList<SongSection> Sections, string? ErrorMessage)
{
    public bool IsSuccess => ErrorMessage == null;
}
