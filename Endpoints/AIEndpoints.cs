using DemoRuleEngine.Models;
using DemoRuleEngine.Services;
using FastEndpoints;

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
    // List of fallback models to try when the primary model is unavailable
    private readonly string[] _fallbackModels;

    public AITranslate(ISchemaService schemaService, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _schemaService = schemaService;
        _httpClient = httpClientFactory.CreateClient();
        
        var provider = config["AIProvider"] ?? "Nvidia";
        var section = config.GetSection(provider);

        var baseUrl = section["BaseUrl"] ?? (provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase)
            ? "https://openrouter.ai/api/v1/"
            : "https://integrate.api.nvidia.com/v1/");
        _httpClient.BaseAddress = new Uri(baseUrl);

        var timeoutSeconds = section.GetValue<int?>("TimeoutSeconds") ?? 100;
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        _apiKey = section["ApiKey"] ?? "";
        _model = section["Model"] ?? (provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase)
            ? "openai/gpt-oss-120b:free"
            : "openai/gpt-oss-120b");

        _fallbackModels = section.GetSection("FallbackModels").Get<string[]>() ?? Array.Empty<string>();

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            
            if (provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
            {
                _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost");
                _httpClient.DefaultRequestHeaders.Add("X-Title", "Demo Rule Engine");
            }
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

        if (string.IsNullOrEmpty(_apiKey) || 
            _apiKey == "YOUR_OPENROUTER_API_KEY_HERE" || 
            _apiKey == "YOUR_NVIDIA_API_KEY_HERE" || 
            _apiKey == "")
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
            // Build a combined list of the primary model followed by any configured fallbacks
            var modelCandidates = new List<string> { _model };
            modelCandidates.AddRange(_fallbackModels);

            OpenAIResponse? finalResult = null;
            string? usedModel = null;
            HttpResponseMessage? failedResponse = null;
            string? failedErrorBody = null;

            foreach (var model in modelCandidates)
            {
                // Prepare request payload for the current model
                var openAiRequest = new
                {
                    model = model,
                    messages = new[] {
                        new { role = "system", content = systemMessage },
                        new { role = "user", content = req.Prompt }
                    },
                    temperature = 0,
                    max_tokens = 1000
                };

                var response = await _httpClient.PostAsJsonAsync("chat/completions", openAiRequest, cancellationToken: ct);
                if (response.IsSuccessStatusCode)
                {
                    // Successful call – capture result and break out of the loop
                    finalResult = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken: ct);
                    usedModel = model;
                    break;
                }
                else
                {
                    // Keep the last failure details in case we need to surface them later
                    failedResponse = response;
                    failedErrorBody = await response.Content.ReadAsStringAsync(cancellationToken: ct);
                    Console.WriteLine($"[AITranslate] Model '{model}' failed with {(int)response.StatusCode}. Trying next fallback if available.");
                    // Continue to next model candidate
                }
            }

            if (finalResult == null)
            {
                // All models failed – report the error to the UI
                var statusCode = failedResponse?.StatusCode ?? System.Net.HttpStatusCode.InternalServerError;
                HttpContext.Response.StatusCode = (int)statusCode;

                string errorMessage = $"AI service is currently unavailable ({(int)statusCode}). Please try again.";
                if (!string.IsNullOrEmpty(failedErrorBody))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(failedErrorBody);
                        if (doc.RootElement.TryGetProperty("error", out var errProp) && 
                            errProp.TryGetProperty("message", out var msgProp))
                        {
                            errorMessage = msgProp.GetString() ?? errorMessage;
                        }
                    }
                    catch
                    {
                        // Fallback to generic message if error parsing fails
                    }
                }

                await HttpContext.Response.SendAsync(new { error = errorMessage }, (int)statusCode, cancellation: ct);
                return;
            }

            var expression = finalResult?.choices?[0]?.message?.content?.Trim() ?? "";

            // Clean up backticks or format prefixes
            expression = expression.Replace("```json", "").Replace("```", "").Replace("csharp", "").Trim();

            await Send.ResponseAsync(new TranslationResponse
            {
                Expression = expression,
                Confidence = 1.0
            }, cancellation: ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AITranslate Exception] {ex}");
            HttpContext.Response.StatusCode = 500;
            await HttpContext.Response.SendAsync(new { error = "AI service translation failed. Please try again." }, 500, cancellation: ct);
        }
    }
}
