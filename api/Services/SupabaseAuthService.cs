using System.Net.Http.Headers;
using System.Text.Json;

namespace api.Services;

public interface ISupabaseAuthService
{
    Task<(bool IsAuthenticated, string? UserId, string ErrorMessage)> TryGetAuthenticatedUserIdAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default);
}

public class SupabaseAuthService : ISupabaseAuthService
{
    private static readonly HttpClient HttpClient = new();
    private readonly IConfiguration _configuration;

    public SupabaseAuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<(bool IsAuthenticated, string? UserId, string ErrorMessage)> TryGetAuthenticatedUserIdAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var authHeader = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return (false, null, "Missing or invalid Authorization header.");
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, null, "Missing bearer token.");
        }

        var supabaseUrl = _configuration["Supabase:Url"];
        if (string.IsNullOrWhiteSpace(supabaseUrl))
        {
            return (false, null, "Server is missing Supabase:Url configuration.");
        }

        var apiKey = _configuration["Supabase:PublishableKey"]
                     ?? _configuration["Supabase:AnonKey"]
                     ?? _configuration["Supabase:ServiceKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, null, "Server is missing Supabase API key configuration.");
        }

        using var authRequest = new HttpRequestMessage(HttpMethod.Get, $"{supabaseUrl.TrimEnd('/')}/auth/v1/user");
        authRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        authRequest.Headers.Add("apikey", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await HttpClient.SendAsync(authRequest, cancellationToken);
        }
        catch
        {
            return (false, null, "Unable to verify token with Supabase Auth.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return (false, null, "Invalid or expired token.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var json = JsonDocument.Parse(body);
            var resolvedUserId = json.RootElement.GetProperty("id").GetString();
            return string.IsNullOrWhiteSpace(resolvedUserId)
                ? (false, null, "Supabase Auth returned an empty user id.")
                : (true, resolvedUserId, string.Empty);
        }
        catch
        {
            return (false, null, "Invalid response from Supabase Auth.");
        }
    }
}
