// Services/NotificationService.cs
using CoffeeShopApi.Models;

namespace CoffeeShopApi.Services
{
    public class NotificationService
    {
        private readonly IConfiguration _configuration;
        private readonly TwilioService _twilioService;

        public NotificationService(IConfiguration configuration, TwilioService twilioService)
        {
            _configuration = configuration;
            _twilioService = twilioService;
        }

        public async Task SendOrderNotificationAsync(Order order)
        {
            var adminPhone = _configuration["Twilio:AdminPhoneNumber"];
            if (string.IsNullOrEmpty(adminPhone))
            {
                return;
            }
            try
            {
                var itemCount = order.OrderItems?.Sum(oi => oi.Quantity) ?? 0;
                var body = $"Roast 66: New Order #{order.Id} - {order.CustomerName} ({order.CustomerPhone}), {itemCount} items. Track at your site.";
                await _twilioService.SendSmsAsync(adminPhone, body);
            }
            catch (Exception)
            {
                // Log but do not fail the order if SMS fails
            }
        }
    }
}
