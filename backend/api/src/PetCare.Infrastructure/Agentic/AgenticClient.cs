using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PetCare.Application.DTOs.Agentic;
using PetCare.Application.Interfaces;

namespace PetCare.Infrastructure.Agentic;

/// <summary>
/// Typed HttpClient for the internal Agentic Service. Sends the shared
/// X-Internal-Key credential and forwards the caller's bearer token so
/// the service's backend reads keep the caller's role/org scope.
/// </summary>
public class AgenticClient : IAgenticClient
{
    internal const string InternalKeyHeader = "X-Internal-Key";

    private readonly HttpClient _httpClient;
    private readonly AgenticServiceOptions _options;
    private readonly ILogger<AgenticClient> _logger;

    public AgenticClient(
        HttpClient httpClient,
        IOptions<AgenticServiceOptions> options,
        ILogger<AgenticClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task<AgenticServiceResult> AnalyzeConsultationAsync(
        string consultationId, string? bearerToken, CancellationToken cancellationToken = default) =>
        PostAsync($"api/agents/consultation-analysis/{Uri.EscapeDataString(consultationId)}", bearerToken, cancellationToken);

    public Task<AgenticServiceResult> AnalyzeDiagnosisAsync(
        string examinationId, string? bearerToken, CancellationToken cancellationToken = default) =>
        PostAsync($"api/agents/diagnosis-analysis/{Uri.EscapeDataString(examinationId)}", bearerToken, cancellationToken);

    public Task<AgenticServiceResult> PlanSchedulingAsync(
        string requestId, string? bearerToken, CancellationToken cancellationToken = default) =>
        PostAsync($"api/agents/scheduling-planning/{Uri.EscapeDataString(requestId)}", bearerToken, cancellationToken);

    public Task<AgenticServiceResult> PlanInventoryAsync(
        string treatmentRecordId, string? bearerToken, CancellationToken cancellationToken = default) =>
        PostAsync($"api/agents/inventory-planning/{Uri.EscapeDataString(treatmentRecordId)}", bearerToken, cancellationToken);

    private async Task<AgenticServiceResult> PostAsync(
        string relativePath, string? bearerToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return AgenticServiceResult.Failed("agentic_not_configured");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);
        if (!string.IsNullOrWhiteSpace(_options.InternalKey))
        {
            request.Headers.TryAddWithoutValidation(InternalKeyHeader, _options.InternalKey);
        }
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.TryAddWithoutValidation("Authorization", bearerToken);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return AgenticServiceResult.Ok((int)response.StatusCode, body);
            }

            _logger.LogWarning("Agentic service call {Path} failed with status {Status}",
                relativePath, (int)response.StatusCode);
            return AgenticServiceResult.Failed(MapError(response.StatusCode), (int)response.StatusCode);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Agentic service call {Path} timed out", relativePath);
            return AgenticServiceResult.Failed("agentic_timeout");
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Agentic service call {Path} could not reach the service", relativePath);
            return AgenticServiceResult.Failed("agentic_unavailable");
        }
    }

    private static string MapError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "agentic_unauthorized",
        HttpStatusCode.Forbidden => "agentic_forbidden",
        HttpStatusCode.NotFound => "agentic_not_found",
        HttpStatusCode.ServiceUnavailable => "agentic_unavailable",
        >= HttpStatusCode.InternalServerError => "agentic_error",
        _ => "agentic_bad_response",
    };
}
