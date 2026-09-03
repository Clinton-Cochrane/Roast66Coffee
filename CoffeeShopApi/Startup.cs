using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CoffeeShopApi.Data;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Security.Claims;
using CoffeeShopApi.Services.Payments;
using CoffeeShopApi.Services.Sms;
using CoffeeShopApi.Health;
using CoffeeShopApi.Middleware;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using CoffeeShopApi.Models;
using CoffeeShopApi.Security;
using System.IdentityModel.Tokens.Jwt;

namespace CoffeeShopApi
{
    /// <summary>
    /// Application composition root. Production uses PostgreSQL and strict security
    /// configuration; the Testing environment swaps only persistence and rate-limit
    /// sizes while retaining the real HTTP middleware and authorization pipeline.
    /// </summary>
    public class Startup
    {
        private readonly IWebHostEnvironment _env;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _env = env;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            SecurityConfiguration.Validate(Configuration, _env);
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(PostgresConnectionString.Build(
                    Configuration.GetConnectionString("DefaultConnection"))));

            services.AddIdentityCore<StaffUser>(options =>
                {
                    options.Password.RequiredLength = 12;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    options.User.RequireUniqueEmail = false;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            services.AddScoped<MenuService>();
            services.AddSingleton<IDefaultMenuProvider, DefaultMenuProvider>();
            services.AddScoped<OrderService>();
            services.AddScoped<NotificationService>();
            services.AddScoped<NotificationSettingsService>();
            services.AddScoped<ISmsSender, DisabledSmsSender>();
            services.AddScoped<OrderEmailNotificationService>();
            services.AddOptions<DataRetentionOptions>()
                .Bind(Configuration.GetSection(DataRetentionOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<DataRetentionService>();
            services.AddScoped<StaffPushNotificationService>();
            services.AddOptions<StaffPushOptions>()
                .Bind(Configuration.GetSection(StaffPushOptions.SectionName))
                .ValidateDataAnnotations()
                .Validate(
                    options => options.DeduplicationCapacity >= options.QueueCapacity,
                    "Push:DeduplicationCapacity must be at least Push:QueueCapacity.")
                .Validate(
                    options => options.DeduplicationWindow > TimeSpan.Zero,
                    "Push:DeduplicationWindow must be positive.")
                .Validate(
                    options => options.RequestTimeout > TimeSpan.Zero &&
                               options.RequestTimeout <= TimeSpan.FromSeconds(30),
                    "Push:RequestTimeout must be between zero and 30 seconds.")
                .Validate(
                    options => options.RetryDelay >= TimeSpan.Zero &&
                               options.RetryDelay <= TimeSpan.FromSeconds(5),
                    "Push:RetryDelay must be between zero and 5 seconds.")
                .ValidateOnStart();
            services.AddSingleton<StaffPushNotificationQueue>();
            services.AddSingleton<IStaffPushNotificationQueue>(provider =>
                provider.GetRequiredService<StaffPushNotificationQueue>());
            services.AddHostedService<StaffPushNotificationWorker>();
            services.AddHttpClient<IStaffPushSender, WebPushStaffPushSender>((provider, client) =>
            {
                var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<StaffPushOptions>>().Value;
                client.Timeout = options.RequestTimeout;
            });
            services.AddScoped<PaymentService>();
            services.AddScoped<IPaymentGateway, StripePaymentGateway>();
            services.AddScoped<SupportEmailService>();
            services.AddScoped<StaffTokenService>();
            services.AddScoped<AuditEventFactory>();
            services.AddScoped<StaffAccountService>();
            services.AddHostedService<DataRetentionWorker>();
            services.AddHttpClient();
            services.AddScoped<IDatabaseReadinessProbe, EfCoreDatabaseReadinessProbe>();
            services.AddHealthChecks()
                .AddCheck<DatabaseReadinessHealthCheck>(
                    "database",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: [ReadinessHealthCheckOptions.RequiredTag],
                    timeout: TimeSpan.FromSeconds(3));

            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                });

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.HttpContext.Response.WriteAsync(
                        "Too many requests. Please try again later.", cancellationToken);
                };

                // The large Testing defaults prevent unrelated integration tests from
                // sharing and exhausting a limiter. Targeted tests override them with
                // small values so the real rejection middleware is still exercised.
                var permitLogin = _env.IsEnvironment("Testing")
                    ? Configuration.GetValue("Testing:RateLimits:LoginPermitLimit", 1000)
                    : 5;
                var permitOrder = _env.IsEnvironment("Testing")
                    ? Configuration.GetValue("Testing:RateLimits:OrderPermitLimit", 1000)
                    : 30;
                var permitForgotPassword = _env.IsEnvironment("Testing") ? 1000 : 3;
                var permitPublicTracking = _env.IsEnvironment("Testing") ? 1000 : 20;

                options.AddPolicy("Login", context =>
                {
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLogin,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
                });
                options.AddPolicy("Order", context =>
                {
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitOrder,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
                });
                options.AddPolicy("ForgotPassword", context =>
                {
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitForgotPassword,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0
                    });
                });
                options.AddPolicy("PublicTracking", context =>
                {
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitPublicTracking,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
                });
            });

            services.AddCors(options =>
            {
                var allowedOrigins = Configuration["AllowedOrigins"];
                if (!string.IsNullOrEmpty(allowedOrigins))
                {
                    options.AddPolicy("CorsPolicy",
                        builder => builder.WithOrigins(allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                         .AllowAnyMethod()
                                         .AllowAnyHeader());
                }
                else if (_env.IsDevelopment() || _env.IsEnvironment("Testing"))
                {
                    options.AddPolicy("CorsPolicy",
                        builder => builder.AllowAnyOrigin()
                                         .AllowAnyMethod()
                                         .AllowAnyHeader());
                }
                else
                {
                    throw new InvalidOperationException(
                        "AllowedOrigins must be configured in production. Set the AllowedOrigins " +
                        "configuration value or environment variable to a comma-separated list of allowed frontend URLs.");
                }
            });

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = JwtTokenSettings.GetIssuer(Configuration),
                    ValidAudience = JwtTokenSettings.GetAudience(Configuration),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                        Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured."))),
                    // Map JWT role/name claims so [Authorize(Roles = "Admin")] and IsInRole work with handler defaults.
                    RoleClaimType = StaffClaimTypes.Role,
                    NameClaimType = JwtRegisteredClaimNames.Name
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        if (principal == null)
                        {
                            context.Fail("Staff principal is missing.");
                            return;
                        }

                        if (principal.HasClaim(StaffClaimTypes.LegacyShared, "true"))
                        {
                            if (!Configuration.GetValue("Authentication:LegacySharedLoginEnabled", false))
                            {
                                context.Fail("Legacy staff authentication is disabled.");
                            }
                            return;
                        }

                        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
                        var tokenStamp = principal.FindFirstValue(StaffClaimTypes.SecurityStamp);
                        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tokenStamp))
                        {
                            context.Fail("Staff identity claims are incomplete.");
                            return;
                        }

                        var userManager = context.HttpContext.RequestServices
                            .GetRequiredService<UserManager<StaffUser>>();
                        var user = await userManager.FindByIdAsync(userId);
                        if (user == null || !user.IsActive ||
                            !string.Equals(user.SecurityStamp, tokenStamp, StringComparison.Ordinal))
                        {
                            context.Fail("Staff session has been revoked.");
                        }
                    }
                };
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseMiddleware<SafeRequestLoggingMiddleware>();

            app.UseRateLimiter();

            app.UseCors("CorsPolicy");

            app.UseAuthentication(); // Must be before UseAuthorization
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks(
                    "/api/health/ready",
                    ReadinessHealthCheckOptions.Create());
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Welcome to the Coffee Shop API!");
                });
            });
        }
    }
}
