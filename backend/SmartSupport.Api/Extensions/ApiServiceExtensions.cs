using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SmartSupport.Api.Infrastructure;
using SmartSupport.Application.Common.Interfaces;
using SmartSupport.Application.Documents;

namespace SmartSupport.Api.Extensions;

/// <summary>Everything the HTTP layer needs. Keeps Program.cs short and readable.</summary>
public static class ApiServiceExtensions
{
    public const string CorsPolicy = "Frontend";

    // Rate-limit policy names, used by [EnableRateLimiting] on endpoints.
    public const string AskPolicy = "ask";
    public const string UploadPolicy = "upload";
    public const string JoinPolicy = "join";

    // Used when Cors:AllowedOrigins isn't configured.
    private static readonly string[] DefaultAllowedOrigins =
    {
        "https://smart-support-rag-engine.vercel.app",
        "http://localhost:4200"
    };

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddHealthChecks();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // One place that turns exceptions into consistent JSON error responses (RFC 7807 problem details).
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        AddGoogleAuthentication(services, configuration);
        AddCors(services, configuration);
        AddRateLimiting(services);

        // Railway terminates HTTPS at its proxy; trust its forwarded headers for the client IP and scheme.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // Uploads: reject oversized multipart bodies before they're buffered.
        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            options.MultipartBodyLengthLimit = DocumentRules.MaxUploadBytes + 64 * 1024);

        return services;
    }

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        else
        {
            app.UseHsts();
        }

        app.UseSecurityHeaders();
        app.UseRouting();
        app.UseCors(CorsPolicy);
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapControllers().RequireAuthorization();

        return app;
    }

    private static void AddGoogleAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var googleClientId = configuration["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(googleClientId))
            throw new InvalidOperationException("Missing 'Authentication:Google:ClientId'.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Google ID tokens: signature, issuer, audience (our client id) and expiry are all checked.
                options.Authority = "https://accounts.google.com";
                options.Audience = googleClientId;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
                    ValidateAudience = true,
                    ValidAudience = googleClientId,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Authentication");
                        logger.LogInformation("Rejected token: {Reason}", context.Exception.GetType().Name);
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
    }

    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        // Only our own frontends may call the API from a browser (instead of "any origin").
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins == null || origins.Length == 0)
            origins = DefaultAllowedOrigins;

        services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type")));
    }

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "You're sending requests too quickly. Please wait a moment and try again."
                };
                problem.Extensions["message"] = problem.Detail;
                await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            };

            // Overall cap per user (or per IP when signed out).
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1)
                }));

            // Each question costs LLM + embedding calls.
            options.AddPolicy(AskPolicy, context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));

            // Uploads are expensive (parsing + embedding).
            options.AddPolicy(UploadPolicy, context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

            // Slows down guessing invite codes.
            options.AddPolicy(JoinPolicy, context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(15) }));
        });
    }

    private static string PartitionKey(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "anonymous";

    private static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            await next();
        });
}
