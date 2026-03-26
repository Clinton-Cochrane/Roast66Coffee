using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Services;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace CoffeeShopApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly StripePaymentService _stripePaymentService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        StripePaymentService stripePaymentService,
        IConfiguration configuration,
        ILogger<PaymentsController> logger)
    {
        _stripePaymentService = stripePaymentService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("checkout-session")]
    public async Task<IActionResult> CreateCheckoutSession(
        [FromBody] CheckoutSessionRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!_stripePaymentService.IsConfigured())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Stripe is not configured for this environment."
            });
        }

        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey.Trim();
        var (checkoutUrl, sessionId) = await _stripePaymentService.CreateCheckoutSessionAsync(
            request,
            key,
            cancellationToken);

        return Ok(new { checkoutUrl, sessionId });
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        string json;
        using (var reader = new StreamReader(Request.Body))
        {
            json = await reader.ReadToEndAsync(cancellationToken);
        }

        var stripeSignature = Request.Headers["Stripe-Signature"];
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid Stripe webhook signature.");
            return BadRequest();
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
            {
                var session = stripeEvent.Data.Object as Session;
                if (session != null)
                {
                    await _stripePaymentService.HandleCheckoutCompletedAsync(
                        session.Id,
                        session.PaymentIntentId,
                        cancellationToken);
                }
                break;
            }
            case EventTypes.PaymentIntentPaymentFailed:
            {
                var intent = stripeEvent.Data.Object as PaymentIntent;
                if (intent != null)
                {
                    await _stripePaymentService.HandlePaymentFailedAsync(intent.Id, cancellationToken);
                }
                break;
            }
        }

        return Ok();
    }
}
