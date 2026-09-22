using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SelfRestaurant.Gateway.Api.Services;

public abstract class ApiClientBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    protected readonly HttpClient Http;

    protected ApiClientBase(HttpClient http)
    {
        Http = http;
    }

    protected async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken, HttpClient? client = null)
    {
        var response = await (client ?? Http).GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    protected async Task PostAsync<TRequest>(string url, TRequest request, CancellationToken cancellationToken, HttpClient? client = null)
    {
        var response = await (client ?? Http).PostAsJsonAsync(url, request, JsonOptions, cancellationToken);
        await EnsureSuccess(response);
    }

    protected async Task<TResponse?> PostForAsync<TRequest, TResponse>(string url, TRequest request, CancellationToken cancellationToken, HttpClient? client = null)
    {
        var response = await (client ?? Http).PostAsJsonAsync(url, request, JsonOptions, cancellationToken);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    protected async Task PutAsync<TRequest>(string url, TRequest request, CancellationToken cancellationToken, HttpClient? client = null)
    {
        var response = await (client ?? Http).PutAsJsonAsync(url, request, JsonOptions, cancellationToken);
        await EnsureSuccess(response);
    }

    protected async Task<TResponse?> PutForAsync<TRequest, TResponse>(string url, TRequest request, CancellationToken cancellationToken, HttpClient? client = null)
    {
        var response = await (client ?? Http).PutAsJsonAsync(url, request, JsonOptions, cancellationToken);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    protected async Task DeleteAsync(string url, CancellationToken cancellationToken, HttpClient? client = null)
    {
        var response = await (client ?? Http).DeleteAsync(url, cancellationToken);
        await EnsureSuccess(response);
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        var message = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        if (!string.IsNullOrWhiteSpace(body))
        {
            string? code = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.String)
                {
                    message = root.GetString() ?? body;
                }
                else if (root.ValueKind == JsonValueKind.Object
                         && root.TryGetProperty("message", out var messageProp)
                         && messageProp.ValueKind == JsonValueKind.String)
                {
                    message = messageProp.GetString() ?? body;
                    if (root.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.String)
                    {
                        code = codeProp.GetString();
                    }
                }
                else
                {
                    message = body;
                }

                throw new ApiClientException(message, (int)response.StatusCode, code, body);
            }
            catch (ApiClientException)
            {
                throw;
            }
            catch
            {
                message = body;
            }
        }

        throw new ApiClientException(message, (int)response.StatusCode, null, body);
    }
}

public sealed class ApiClientException : InvalidOperationException
{
    public ApiClientException(string message, int statusCode, string? code = null, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        ResponseBody = responseBody;
    }

    public int StatusCode { get; }
    public string? Code { get; }
    public string? ResponseBody { get; }
}
