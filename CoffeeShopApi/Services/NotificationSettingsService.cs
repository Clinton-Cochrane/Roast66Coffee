// Services/NotificationSettingsService.cs
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using CoffeeShopApi.Security;

namespace CoffeeShopApi.Services
{
    public class NotificationSettingsService(
        ApplicationDbContext context,
        AuditEventFactory? auditEvents = null)
    {
        private readonly ApplicationDbContext _context = context;
        private readonly AuditEventFactory? _auditEvents = auditEvents;

        public async Task<NotificationSettings?> GetNotificationSettingsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.NotificationSettings
                .OrderBy(ns => ns.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task SaveNotificationSettingsAsync(
            NotificationSettings settings,
            CancellationToken cancellationToken = default,
            StaffActor? actor = null)
        {
            var existingSettings = await GetNotificationSettingsAsync(cancellationToken);
            var changedFields = ChangedFields(existingSettings, settings);
            if (existingSettings != null)
            {
                existingSettings.AdminPhoneNumber = settings.AdminPhoneNumber;
                existingSettings.BaristaPhoneNumber = settings.BaristaPhoneNumber;
                existingSettings.TrailerPhoneNumber = settings.TrailerPhoneNumber;
                existingSettings.AdminEmail = settings.AdminEmail;
                existingSettings.BaristaEmail = settings.BaristaEmail;
                existingSettings.TrailerEmail = settings.TrailerEmail;
                existingSettings.SmsFromAddress = settings.SmsFromAddress;
                _context.NotificationSettings.Update(existingSettings);
            }
            else
            {
                _context.NotificationSettings.Add(settings);
            }
            if (actor != null && _auditEvents != null && changedFields.Count > 0)
            {
                _auditEvents.Add(
                    actor,
                    "notification_settings.changed",
                    "notification_settings",
                    "singleton",
                    new { ChangedFields = changedFields });
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static IReadOnlyList<string> ChangedFields(
            NotificationSettings? current,
            NotificationSettings next)
        {
            var fields = new List<string>();
            if (current?.AdminPhoneNumber != next.AdminPhoneNumber) fields.Add(nameof(next.AdminPhoneNumber));
            if (current?.BaristaPhoneNumber != next.BaristaPhoneNumber) fields.Add(nameof(next.BaristaPhoneNumber));
            if (current?.TrailerPhoneNumber != next.TrailerPhoneNumber) fields.Add(nameof(next.TrailerPhoneNumber));
            if (current?.AdminEmail != next.AdminEmail) fields.Add(nameof(next.AdminEmail));
            if (current?.BaristaEmail != next.BaristaEmail) fields.Add(nameof(next.BaristaEmail));
            if (current?.TrailerEmail != next.TrailerEmail) fields.Add(nameof(next.TrailerEmail));
            if (current?.SmsFromAddress != next.SmsFromAddress) fields.Add(nameof(next.SmsFromAddress));
            return fields;
        }
    }
}
