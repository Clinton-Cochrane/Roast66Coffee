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
using Serilog;
using System.Security.Claims;

namespace CoffeeShopApi
{
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
            if (_env.IsEnvironment("Testing"))
            {
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("IntegrationTestDb"));
            }
            else
            {
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection")));
            }

            services.AddScoped<MenuService>();
            services.AddScoped<OrderService>();
            services.AddScoped<NotificationService>();
            services.AddScoped<NotificationSettingsService>();
            services.AddScoped<TwilioService>();
            services.AddScoped<OrderEmailNotificationService>();
            services.AddScoped<NotificationRetentionService>();
            services.AddScoped<StaffPushNotificationService>();
            services.AddScoped<StripePaymentService>();
            services.AddScoped<SupportEmailService>();
            services.AddSingleton<KeepAliveStateStore>();
            services.AddHostedService<ConnectionWarmupService>();
            services.AddHostedService<NotificationRetentionWorker>();
            services.AddHttpClient();

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

                var permitLogin = _env.IsEnvironment("Testing") ? 1000 : 5;
                var permitOrder = _env.IsEnvironment("Testing") ? 1000 : 30;
                var permitForgotPassword = _env.IsEnvironment("Testing") ? 1000 : 3;

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
            });

            if (!_env.IsEnvironment("Testing"))
            {
                var jwtKey = Configuration["Jwt:Key"];
                if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
                {
                    throw new InvalidOperationException(
                        "Jwt:Key must be configured and at least 32 characters long. " +
                        "Set Jwt__Key (or Jwt:Key) in environment variables or appsettings.");
                }
            }

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
                else if (_env.IsDevelopment())
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
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = JwtTokenSettings.GetIssuer(Configuration),
                    ValidAudience = JwtTokenSettings.GetAudience(Configuration),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:Key"] ?? "default_key")),
                    // Map JWT role/name claims so [Authorize(Roles = "Admin")] and IsInRole work with handler defaults.
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.Name
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

            app.UseSerilogRequestLogging();

            app.UseRouting();

            app.UseRateLimiter();

            app.UseCors("CorsPolicy");

            app.UseAuthentication(); // Must be before UseAuthorization
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Welcome to the Coffee Shop API!");
                });
            });
        }
    }
}