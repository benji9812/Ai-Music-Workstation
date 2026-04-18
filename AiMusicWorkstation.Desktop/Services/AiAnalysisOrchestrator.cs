using AiMusicWorkstation.Shared.Models;
using System.Text.Json;

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
}

public record AnalysisParseResult(AnalysisResult? Result, string RawResponse, AnalysisParseError? Error);

public record AnalysisParseError(string StatusLabel, string MessageBoxText);
