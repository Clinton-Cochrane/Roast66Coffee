// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace CoffeeShopApi.Data
{
    /// <summary>
    /// Relational contract for application-owned data. Order line/add-on menu
    /// relationships use SET NULL so immutable snapshots survive menu replacement;
    /// tracking and idempotency uniqueness are enforced by PostgreSQL, not only code.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<StaffUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<MenuItem> MenuItems { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<NotificationSettings> NotificationSettings { get; set; } = null!;
        public DbSet<NotificationMessage> NotificationMessages { get; set; } = null!;
        public DbSet<StaffPushSubscription> StaffPushSubscriptions { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<AuditEvent> AuditEvents { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<StaffUser>().ToTable("staffusers");
            modelBuilder.Entity<IdentityRole>().ToTable("staffroles");
            modelBuilder.Entity<IdentityUserRole<string>>().ToTable("staffuserroles");
            modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("staffuserclaims");
            modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("staffuserlogins");
            modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("staffroleclaims");
            modelBuilder.Entity<IdentityUserToken<string>>().ToTable("staffusertokens");
            modelBuilder.Entity<AuditEvent>()
                .HasIndex(audit => new { audit.EntityType, audit.EntityId, audit.Action, audit.OccurredUtc })
                .HasDatabaseName("ix_auditevents_entity_action_occurredutc");
            modelBuilder.Entity<StaffPushSubscription>()
                .HasOne(subscription => subscription.StaffUser)
                .WithMany()
                .HasForeignKey(subscription => subscription.StaffUserId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Order>()
                .Property(order => order.TrackingToken)
                .HasMaxLength(43)
                .IsRequired();
            modelBuilder.Entity<Order>()
                .HasIndex(order => order.TrackingToken)
                .IsUnique();
            modelBuilder.Entity<Order>()
                .HasIndex(order => order.IdempotencyKey)
                .IsUnique()
                .HasDatabaseName("ux_orders_idempotency_key");
            modelBuilder.Entity<Order>()
                .HasIndex(order => new
                {
                    order.OrderStatus,
                    order.CompletedUtc,
                    order.OrderDate,
                    order.Id
                })
                .HasDatabaseName("ix_orders_admin_history");
            modelBuilder.Entity<Payment>()
                .HasIndex(payment => new { payment.Provider, payment.IdempotencyKey });
            modelBuilder.Entity<Payment>()
                .HasIndex(payment => new { payment.Provider, payment.ProviderCheckoutId })
                .IsUnique();
            modelBuilder.Entity<Payment>()
                .HasOne(payment => payment.Order)
                .WithMany()
                .HasForeignKey(payment => payment.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<OrderItem>()
                .Property(orderItem => orderItem.ItemName)
                .IsRequired();
            modelBuilder.Entity<OrderItem>()
                .Property(orderItem => orderItem.ItemDescription)
                .IsRequired();
            modelBuilder.Entity<OrderItem>()
                .HasOne(orderItem => orderItem.MenuItem)
                .WithMany()
                .HasForeignKey(orderItem => orderItem.MenuItemId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<AddOn>()
                .Property(addOn => addOn.ItemName)
                .IsRequired();
            modelBuilder.Entity<AddOn>()
                .Property(addOn => addOn.ItemDescription)
                .IsRequired();
            modelBuilder.Entity<AddOn>()
                .HasOne(addOn => addOn.MenuItem)
                .WithMany()
                .HasForeignKey(addOn => addOn.MenuItemId)
                .OnDelete(DeleteBehavior.SetNull);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            RejectAuditMutations();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            RejectAuditMutations();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void RejectAuditMutations()
        {
            if (ChangeTracker.Entries<AuditEvent>().Any(entry =>
                    entry.State is EntityState.Modified or EntityState.Deleted))
            {
                throw new InvalidOperationException("Audit events are append-only.");
            }
        }
    }
}
