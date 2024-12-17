// Services/NotificationService.cs
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
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
            TwilioClient.Init(_configuration["Twilio:AccountSID"], _configuration["Twilio:AuthToken"]);
        }

        public async Task SendOrderNotificationAsync(Order order)
        {
            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(_configuration["Twilio:AdminPhoneNumber"]),
                from: new PhoneNumber(_configuration["Twilio:FromPhoneNumber"]),
                body: $"New Order: {order.CustomerName} ordered {order.OrderItems.Count} items.");

            await _twilioService.SendSmsAsync("8449804273", "test");
            
            // Log the message SID (optional)
            Console.WriteLine($"Message SID: {message.Sid}");
        }
    }
}
