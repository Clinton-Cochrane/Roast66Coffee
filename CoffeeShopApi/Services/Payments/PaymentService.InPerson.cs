using System.Globalization;
using System.Text.Json;
using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Security;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services.Payments;

public sealed partial class PaymentService
{
    public async Task<InPersonPaymentResult> StartInPersonPaymentAsync(
        int orderId, StaffActor actor, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0) throw new ArgumentOutOfRangeException(nameof(orderId));
        Payment payment;
        // Commit the reservation before contacting a provider. A second request can
        // replay it without holding an order lock throughout an external network call.
        await using (var transaction = await BeginOrderPaymentTransactionAsync(orderId, cancellationToken))
        {
            var order = await _orderService.GetOrderByIdAsync(orderId, cancellationToken)
                ?? throw new InPersonPaymentOrderNotFoundException();
            await _context.Entry(order).ReloadAsync(cancellationToken);
            if (order.PaidUtc != null)
                throw new InPersonPaymentConflictException("This order is already paid.");
            var pending = await _context.Payments.AsNoTracking().SingleOrDefaultAsync(
                payment => payment.OrderId == orderId && payment.IsInPerson && payment.Status == PaymentStatuses.Pending,
                cancellationToken);
            if (pending != null) return InPersonResult(pending);

            var gateway = GetInPersonGateway();
            var amount = BuildLineItemsFromOrder(order).Sum(item => item.UnitPrice * item.Quantity);
            if (amount <= 0) throw new InvalidOperationException("This order has no positive billable total.");
            payment = new Payment
            {
                Provider = gateway.ProviderName, IsInPerson = true, Method = "card",
                Status = PaymentStatuses.Pending, Amount = amount, Currency = "USD", OrderId = order.Id,
                CustomerName = order.CustomerName, CustomerPhone = order.CustomerPhone ?? string.Empty,
                PayloadJson = JsonSerializer.Serialize(new CheckoutSessionPayload { ExistingOrderId = order.Id })
            };
            payment.IdempotencyKey = payment.Id.ToString("N");
            _context.Payments.Add(payment);
            _auditEvents.Add(actor, "payment.in-person.started", "order", orderId.ToString(CultureInfo.InvariantCulture),
                new { PaymentId = payment.Id, payment.Provider, payment.Amount, payment.Currency }, payment.CreatedUtc);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
        }

        InPersonGatewayResult result;
        try
        {
            result = await GetInPersonGateway().StartPaymentAsync(new InPersonGatewayRequest(
                payment.Id, orderId, payment.Amount, payment.Currency,
                new Dictionary<string, string>
                {
                    ["payment_id"] = payment.Id.ToString("N"),
                    ["order_id"] = orderId.ToString(CultureInfo.InvariantCulture),
                    ["staff_actor_id"] = actor.UserId ?? string.Empty
                }), payment.IdempotencyKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("In-person payment {PaymentId} start could not be confirmed. Failure type: {FailureType}.",
                payment.Id, exception.GetType().Name);
            // An HTTP timeout may occur after the charge was initiated. Only a verified
            // failure can free this reservation for another attempt.
            throw new PaymentProviderUnavailableException("Could not confirm the card payment start. The payment remains pending; retrying will return that attempt.");
        }

        if (result == null || !Enum.IsDefined(result.Status))
            throw new PaymentProviderUnavailableException("The card provider returned an unknown status. The payment remains pending.");
        // A webhook can arrive during StartPaymentAsync. Use its persisted terminal
        // state before applying the adapter response, preserving paid over failed.
        await _context.Entry(payment).ReloadAsync(cancellationToken);
        await ApplyGatewayEventAsync(payment, new GatewayPaymentEvent(
            payment.Id, null, result.ProviderPaymentId, result.Status, "card"), cancellationToken);
        await _context.Entry(payment).ReloadAsync(cancellationToken);
        return InPersonResult(payment);
    }

    public async Task<InPersonPaymentResult?> GetInPersonPaymentAsync(
        Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _context.Payments.AsNoTracking().SingleOrDefaultAsync(
            payment => payment.Id == paymentId && payment.IsInPerson, cancellationToken);
        return payment?.OrderId == null ? null : InPersonResult(payment);
    }

    private IInPersonPaymentGateway GetInPersonGateway()
    {
        var provider = _configuration["Payments:InPersonProvider"]?.Trim();
        if (string.IsNullOrWhiteSpace(provider) || !_inPersonGateways.TryGetValue(provider, out var gateway) || !gateway.IsConfigured())
            throw new PaymentProviderUnavailableException("Card payments are not configured for this environment.");
        return gateway;
    }

    private static InPersonPaymentResult InPersonResult(Payment payment) => new(
        payment.Id, payment.OrderId!.Value, payment.Provider, payment.Status,
        payment.Amount, payment.Currency, payment.CompletedUtc);
}

public sealed class InPersonPaymentOrderNotFoundException : Exception;
public sealed class InPersonPaymentConflictException(string message) : Exception(message);
