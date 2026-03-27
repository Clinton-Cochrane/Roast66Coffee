// Services/NotificationSettingsService.cs
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services
{
    public class NotificationSettingsService(ApplicationDbContext context)
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<NotificationSettings?> GetNotificationSettingsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.NotificationSettings
                .OrderBy(ns => ns.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task SaveNotificationSettingsAsync(NotificationSettings settings, CancellationToken cancellationToken = default)
        {
            var existingSettings = await GetNotificationSettingsAsync(cancellationToken);
            if (existingSettings != null)
            {
                existingSettings.AdminPhoneNumber = settings.AdminPhoneNumber;
                existingSettings.BaristaPhoneNumber = settings.BaristaPhoneNumber;
                existingSettings.TrailerPhoneNumber = settings.TrailerPhoneNumber;
                existingSettings.TwilioFromPhoneNumber = settings.TwilioFromPhoneNumber;
                _context.NotificationSettings.Update(existingSettings);
            }
            else
            {
                _context.NotificationSettings.Add(settings);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
