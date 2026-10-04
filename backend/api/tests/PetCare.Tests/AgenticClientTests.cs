using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PetCare.Infrastructure.Agentic;
using Xunit;

namespace PetCare.Tests;

public class AgenticClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }
        public Exception? Failure { get; set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (Failure is not null)
            {
                throw Failure;
            }
            return Task.FromResult(Responder!(request));
        }
    }

    private static AgenticClient CreateClient(
        StubHandler handler, string baseUrl = "http://agentic.test", string internalKey = "k")
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        var options = Options.Create(new AgenticServiceOptions
        {
            BaseUrl = baseUrl,
            InternalKey = internalKey,
        });
        return new AgenticClient(http, options, NullLogger<AgenticClient>.Instance);
    }

    [Fact]
    public async Task Success_ReturnsContent()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"priority\":\"High\"}")
            }
        };
        var client = CreateClient(handler);

        var result = await client.AnalyzeConsultationAsync("c1", "Bearer t");

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("High", result.Content);
    }

    [Fact]
    public async Task SendsInternalKey_AndForwardsBearerToken()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            }
        };
        var client = CreateClient(handler, internalKey: "secret-1");

        await client.AnalyzeDiagnosisAsync("e1", "Bearer caller-jwt");

        Assert.Equal("secret-1",
            Assert.Single(handler.LastRequest!.Headers.GetValues("X-Internal-Key")));
        Assert.Equal("Bearer caller-jwt",
            Assert.Single(handler.LastRequest.Headers.GetValues("Authorization")));
        Assert.Equal("api/agents/diagnosis-analysis/e1",
            handler.LastRequest.RequestUri!.AbsolutePath.TrimStart('/'));
    }

    [Fact]
    public async Task Inventory_UsesTreatmentRecordRoute()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            }
        };
        var client = CreateClient(handler);

        await client.PlanInventoryAsync("tr-9", null);

        Assert.Equal("api/agents/inventory-planning/tr-9",
            handler.LastRequest!.RequestUri!.AbsolutePath.TrimStart('/'));
    }

    [Fact]
    public async Task NotConfigured_FailsSafely()
    {
        var handler = new StubHandler();
        var options = Options.Create(new AgenticServiceOptions { BaseUrl = "", InternalKey = "" });
        var client = new AgenticClient(new HttpClient(handler), options, NullLogger<AgenticClient>.Instance);

        var result = await client.PlanSchedulingAsync("r1", null);

        Assert.False(result.Success);
        Assert.Equal("agentic_not_configured", result.Error);
        Assert.Null(handler.LastRequest); // no HTTP call attempted
    }

    [Fact]
    public async Task ServerError_FailsSafely()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        };
        var client = CreateClient(handler);

        var result = await client.AnalyzeConsultationAsync("c1", null);

        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("agentic_error", result.Error);
    }

    [Fact]
    public async Task Unauthorized_FailsSafely()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        };
        var client = CreateClient(handler);

        var result = await client.AnalyzeConsultationAsync("c1", null);

        Assert.False(result.Success);
        Assert.Equal("agentic_unauthorized", result.Error);
    }

    [Fact]
    public async Task Timeout_FailsSafely()
    {
        var handler = new StubHandler { Failure = new TaskCanceledException("timeout") };
        var client = CreateClient(handler);

        var result = await client.AnalyzeDiagnosisAsync("e1", null);

        Assert.False(result.Success);
        Assert.Equal("agentic_timeout", result.Error);
    }

    [Fact]
    public async Task ConnectionFailure_FailsSafely()
    {
        var handler = new StubHandler { Failure = new HttpRequestException("refused") };
        var client = CreateClient(handler);

        var result = await client.PlanInventoryAsync("tr1", null);

        Assert.False(result.Success);
        Assert.Equal("agentic_unavailable", result.Error);
    }
}
