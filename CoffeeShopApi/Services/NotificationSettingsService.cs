// Services/NotificationSettingsService.cs
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services
{
    public class NotificationSettingsService(ApplicationDbContext context)
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<NotificationSettings?> GetNotificationSettingsAsync()
        {
            return await _context.NotificationSettings.OrderBy(ns =>ns.Id).FirstOrDefaultAsync();
        }

        public async Task SaveNotificationSettingsAsync(NotificationSettings settings)
        {
            var existingSettings = await GetNotificationSettingsAsync();
            if (existingSettings != null)
            {
                existingSettings.PhoneNumber = settings.PhoneNumber;
                _context.NotificationSettings.Update(existingSettings);
            }
            else
            {
                _context.NotificationSettings.Add(settings);
            }
            await _context.SaveChangesAsync();
        }
    }
}
