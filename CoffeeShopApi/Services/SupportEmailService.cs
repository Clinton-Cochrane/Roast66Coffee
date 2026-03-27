using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CoffeeShopApi.Services;

public class SupportEmailService
{
    private const string ResendApiUrl = "https://api.resend.com/emails";
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SupportEmailService> _logger;

    public SupportEmailService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<SupportEmailService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_configuration["Resend:ApiKey"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Resend:From"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Support:AlertEmail"]);

    public async Task SendForgotPasswordAlertAsync(string sourceIp, string? note, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Resend:ApiKey"]?.Trim();
        var from = _configuration["Resend:From"]?.Trim();
        var to = _configuration["Support:AlertEmail"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            throw new InvalidOperationException("Forgot-password email is not configured.");
        }

        var payload = new
        {
            from,
            to = new[] { to },
            subject = "Roast66 admin forgot-password request",
            text = BuildBody(sourceIp, note)
        };

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, ResendApiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Resend forgot-password email failed. Status: {StatusCode}. Body: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException("Failed to send forgot-password email.");
        }
    }

    private static string BuildBody(string sourceIp, string? note)
    {
        var safeNote = string.IsNullOrWhiteSpace(note) ? "(none)" : note.Trim();
        return
            "A forgot-password request was submitted for Roast66 admin login.\n\n" +
            $"Time (UTC): {DateTime.UtcNow:O}\n" +
            $"Source IP: {sourceIp}\n" +
            $"Note: {safeNote}\n\n" +
            "Action: Update Admin__Password in Render and share the new password with staff.";
    }
}
