using FastEndpoints;
using DemoRuleEngine.Services;
using DemoRuleEngine.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DemoRuleEngine.Endpoints;

public class GetAISchema : EndpointWithoutRequest<List<FieldMetadata>>
{
    private readonly ISchemaService _schemaService;

    public GetAISchema(ISchemaService schemaService)
    {
        _schemaService = schemaService;
    }

    public override void Configure()
    {
        Get("/api/ai/schema");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.ResponseAsync(_schemaService.GetDataDictionary(), cancellation: ct);
    }
}

public class TranslationRequest
{
    public string Prompt { get; set; } = string.Empty;
}

public class TranslationResponse
{
    public string Expression { get; set; } = string.Empty;
    public double Confidence { get; set; } = 1.0;
    public string Model { get; set; } = string.Empty;
}

public class OpenAIResponse
{
    public Choice[]? choices { get; set; }
    public class Choice { public Message? message { get; set; } }
    public class Message { public string? content { get; set; } }
}

public class AITranslate : Endpoint<TranslationRequest, TranslationResponse>
{
    private readonly ISchemaService _schemaService;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public AITranslate(ISchemaService schemaService, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _schemaService = schemaService;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");

        _apiKey = config["OpenRouter:ApiKey"] ?? "";
        _model = config["OpenRouter:Model"] ?? "meta-llama/llama-3.3-70b-instruct:free";

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "Demo Rule Engine");
        }
    }

    public override void Configure()
    {
        Post("/api/ai/translate");
        AllowAnonymous();
    }

    public override async Task HandleAsync(TranslationRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Prompt))
        {
            await Send.ErrorsAsync(statusCode: 400, cancellation: ct);
            return;
        }

        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "YOUR_OPENROUTER_API_KEY_HERE")
        {
            await Send.ErrorsAsync(statusCode: 500, cancellation: ct);
            return;
        }

        // Layer 3: Cache-friendly prompt layout
        // Static system prompt (identical across requests ? fully cached by LLM providers)
        var staticPrompt = _schemaService.GetStaticSystemPrompt();

        // Dynamic schema (only relevant fields based on user prompt ? partially cacheable)
        var optimizedSchema = _schemaService.GetOptimizedSchemaText(req.Prompt);

        // Assemble: static instructions first (cacheable prefix), then dynamic schema
        var systemMessage = $"{staticPrompt}\n\nAVAILABLE DATA FIELDS:\n{optimizedSchema}";

        try
        {
            var openAiRequest = new
            {
                model = _model,
                messages = new[] {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = req.Prompt }
                },
                temperature = 0,
                max_tokens = 1000
            };

            var response = await _httpClient.PostAsJsonAsync("chat/completions", openAiRequest, cancellationToken: ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken: ct);
                HttpContext.Response.StatusCode = (int)response.StatusCode;
                await Send.ResponseAsync(new TranslationResponse { Expression = $"AI Connection Error: {(int)response.StatusCode}. Details: {errorBody}" }, cancellation: ct);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken: ct);
            var expression = result?.choices?[0]?.message?.content?.Trim() ?? "";

            // Clean up backticks or format prefixes
            expression = expression.Replace("```json", "").Replace("```", "").Replace("csharp", "").Trim();

            await Send.ResponseAsync(new TranslationResponse
            {
                Expression = expression,
                Confidence = 1.0,
                Model = _model
            }, cancellation: ct);
        }
        catch (Exception ex)
        {
            HttpContext.Response.StatusCode = 500;
            await Send.ResponseAsync(new TranslationResponse { Expression = $"AI Connection Failed: {ex.Message}" }, cancellation: ct);
        }
    }
}
