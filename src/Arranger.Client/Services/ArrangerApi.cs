using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts;
using Faemiyah.Common;
using Microsoft.AspNetCore.Components;

namespace Faemiyah.Arranger.Client.Services;

/// <summary>
/// Calls the Arranger API, turning problem responses into messages and a lost sign-in into a trip to the login page.
/// </summary>
public sealed class ArrangerApi
{
    private const string NoContentError = "The server sent no data.";
    private const string UnreachableError = "Could not reach the server. Check your connection and try again.";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly HttpClient _http;
    private readonly NavigationManager _navigation;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrangerApi"/> class.
    /// </summary>
    /// <param name="http">The HTTP client, addressed at the host.</param>
    /// <param name="navigation">The navigation manager.</param>
    public ArrangerApi(HttpClient http, NavigationManager navigation)
    {
        _http = http;
        _navigation = navigation;
    }

    /// <summary>
    /// Create an HTTP client for the API at the given address.
    /// </summary>
    /// <param name="baseAddress">The host address.</param>
    /// <returns>The client.</returns>
    public static HttpClient CreateHttpClient(string baseAddress)
    {
        var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
        http.DefaultRequestHeaders.Add(ApiHeaders.RequestedWith, ApiHeaders.RequestedWithValue);
        return http;
    }

    /// <summary>
    /// GET a value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="path">The relative URI.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult<T>> GetAsync<T>(string path)
        where T : class
    {
        return SendAsync<T>(HttpMethod.Get, path);
    }

    /// <summary>
    /// Send a request with an optional JSON body, and read a value from the response.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The relative URI.</param>
    /// <param name="body">The request body, or <b>null</b> for none.</param>
    /// <returns>The outcome.</returns>
    public Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body = null)
        where T : class
    {
        return SendAsync<T>(method, path, body == null ? null : JsonContent.Create(body, options: JsonOptions));
    }

    /// <summary>
    /// Send a request with the given content, and read a value from the response.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The relative URI.</param>
    /// <param name="content">The request content, or <b>null</b> for none.</param>
    /// <returns>The outcome. A lost connection, a timeout or a response without content is an error.</returns>
    public async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, HttpContent? content)
        where T : class
    {
        try
        {
            using var response = await SendCoreAsync(method, path, content);
            if (!response.IsSuccessStatusCode)
            {
                return new ApiResult<T>(null, await ReadErrorAsync(response));
            }

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return new ApiResult<T>(null, NoContentError);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            return value == null ? new ApiResult<T>(null, NoContentError) : new ApiResult<T>(value, null);
        }
        catch (Exception e) when (IsUnreachable(e))
        {
            return new ApiResult<T>(null, UnreachableError);
        }
    }

    /// <summary>
    /// Send a request whose response has no content.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The relative URI.</param>
    /// <param name="content">The request content, or <b>null</b> for none.</param>
    /// <returns>The error message, or <b>null</b> on success. A lost connection or a timeout is an error.</returns>
    public async Task<string?> SendAsync(HttpMethod method, string path, HttpContent? content = null)
    {
        try
        {
            using var response = await SendCoreAsync(method, path, content);
            return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response);
        }
        catch (Exception e) when (IsUnreachable(e))
        {
            return UnreachableError;
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonConfiguration.ApplyDefaultJsonSerializerOptions(options);
        return options;
    }

    private static bool IsUnreachable(Exception e)
    {
        // A lost connection, or the HttpClient timeout, which is reported as a cancellation.
        return e is HttpRequestException or TaskCanceledException { InnerException: TimeoutException };
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentType?.MediaType == MediaTypeNames.Application.ProblemJson)
        {
            try
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOptions);
                if ((problem?.Detail ?? problem?.Title) is { } message)
                {
                    return message;
                }
            }
            catch (JsonException)
            {
                // An unreadable problem body still has a status code to report.
            }
        }

        return $"The request failed: {(int)response.StatusCode} {response.ReasonPhrase}.";
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, HttpContent? content)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        var response = await _http.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // The cookie expired or was revoked. The login page brings the user back here.
            _navigation.NavigateToLogin();
        }

        return response;
    }
}
