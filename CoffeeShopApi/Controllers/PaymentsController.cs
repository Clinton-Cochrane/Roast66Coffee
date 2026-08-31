using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Services.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoffeeShopApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        PaymentService paymentService,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost("checkout-session")]
    [EnableRateLimiting("Order")]
    public async Task<IActionResult> CreateCheckoutSession(
        [FromBody] CheckoutSessionRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request.ExistingOrderId is null or <= 0)
        {
            return BadRequest(new
            {
                message = "Create the order before starting online payment."
            });
        }

        if (!_paymentService.IsConfigured())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Online payments are not configured for this environment."
            });
        }

        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey.Trim();
        try
        {
            var checkout = await _paymentService.CreateCheckoutAsync(
                request,
                key,
                cancellationToken: cancellationToken);

            return Ok(new
            {
                checkout.CheckoutUrl,
                checkout.CheckoutId,
                sessionId = checkout.CheckoutId,
                checkout.Provider
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (PaymentProviderUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    [HttpPost("webhook")]
    public Task<IActionResult> HandleDefaultWebhook(CancellationToken cancellationToken) =>
        HandleWebhook(null, cancellationToken);

    [HttpPost("{provider}/webhook")]
    public async Task<IActionResult> HandleProviderWebhook(
        string provider,
        CancellationToken cancellationToken) =>
        await HandleWebhook(provider, cancellationToken);

    private async Task<IActionResult> HandleWebhook(
        string? provider,
        CancellationToken cancellationToken)
    {
        string json;
        using (var reader = new StreamReader(Request.Body))
        {
            json = await reader.ReadToEndAsync(cancellationToken);
        }

        try
        {
            var headers = Request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);
            await _paymentService.HandleWebhookAsync(
                provider,
                json,
                headers,
                cancellationToken);
            return Ok();
        }
        catch (PaymentWebhookException ex)
        {
            _logger.LogWarning(
                "Invalid {Provider} payment webhook. Failure type: {FailureType}.",
                provider ?? _paymentService.DefaultProviderName,
                ex.GetType().Name);
            return BadRequest();
        }
        catch (PaymentWebhookRetryException ex)
        {
            _logger.LogWarning(
                "Payment webhook arrived before its local payment record was available. Failure type: {FailureType}.",
                ex.GetType().Name);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (PaymentProviderUnavailableException ex)
        {
            _logger.LogWarning(
                "Payment webhook provider is unavailable. Failure type: {FailureType}.",
                ex.GetType().Name);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
