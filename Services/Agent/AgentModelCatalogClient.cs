using System.Net.Http.Headers;
using System.Text.Json;
using CxShell.Models;
using CxShell.Services;

namespace CxShell.Services.Agent;

public sealed record AgentModelCatalogResult(
    bool Success,
    IReadOnlyList<string> Models,
    string? Error = null);

/// <summary>Reads a bounded provider model catalog without exposing API keys.</summary>
public sealed class AgentModelCatalogClient
{
    private const int MaximumResponseBytes = 2 * 1024 * 1024;
    private static readonly HttpClient SharedHttpClient = new();
    private readonly HttpClient _httpClient;

    public AgentModelCatalogClient(
        HttpClient? httpClient = null,
        Func<ProxySettings?>? globalProxyProvider = null)
    {
        _httpClient = httpClient ?? (globalProxyProvider == null
            ? SharedHttpClient
            : NetworkProxyHttpClientFactory.Create(globalProxyProvider));
    }

    public async Task<AgentModelCatalogResult> FetchAsync(
        AgentProviderSettings provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var validation = AgentProviderConfiguration.Validate(provider);
        if (!validation.IsValid)
            return new(false, [], validation.Message);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            AgentProviderConfiguration.BuildModelsUri(provider));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var apiKey = AgentProviderConfiguration.GetApiKey(provider);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            if (AgentProviderConfiguration.IsAnthropicProvider(provider))
            {
                request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
                request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Min(provider.RequestTimeoutSeconds, 30)));
        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new(false, [], $"Model catalog returned HTTP {(int)response.StatusCode}.");

            if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
                return new(false, [], "The provider model catalog response is too large.");

            var body = await ReadBoundedAsync(response, timeout.Token).ConfigureAwait(false);
            if (body == null)
                return new(false, [], "The provider model catalog response is too large.");

            using var document = JsonDocument.Parse(
                body,
                new JsonDocumentOptions { MaxDepth = 16 });
            var data = FindModelArray(document.RootElement);
            if (data.ValueKind != JsonValueKind.Array)
                return new(false, [], "The provider returned an unsupported model catalog format.");

            var models = data.EnumerateArray()
                .Select(ReadModelId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(100)
                .ToArray();
            return new(true, models);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, [], "Model catalog request timed out.");
        }
        catch (JsonException)
        {
            return new(false, [], "The provider returned invalid model catalog JSON.");
        }
        catch (HttpRequestException exception)
        {
            return new(false, [], $"Model catalog request failed: {exception.Message}");
        }
    }

    private static JsonElement FindModelArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root;

        if (root.ValueKind != JsonValueKind.Object)
            return default;

        foreach (var propertyName in new[] { "data", "models" })
        {
            if (root.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.Array)
                return value;
        }

        return default;
    }

    private static string? ReadModelId(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
            return item.GetString();

        if (item.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var propertyName in new[] { "id", "model", "name" })
        {
            if (item.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    private static async Task<byte[]?> ReadBoundedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream(Math.Min(MaximumResponseBytes, 128 * 1024));
        var buffer = new byte[32 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return output.ToArray();
            if (output.Length + read > MaximumResponseBytes)
                return null;

            output.Write(buffer, 0, read);
        }
    }
}
