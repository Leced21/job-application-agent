using System.Security.Claims;
using System.Data;
using JobApplicationAgent.Gateway.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Gateway.Identity;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints,
        IConfiguration configuration, IHostEnvironment environment)
    {
        endpoints.MapGet("/auth/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new AntiforgeryResponse(tokens.RequestToken!));
        }).AllowAnonymous();

        endpoints.MapGet("/auth/session", async (HttpContext context, UserManager<ApplicationUser> users) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var user = context.User;
            var hasUsers = await users.Users.AnyAsync();
            var registrationEnabled = AccountOwnerIdResolver.CanRegister(
                configuration.GetValue("Identity:AllowRegistration", environment.IsDevelopment()),
                hasUsers,
                configuration["Identity:InitialOwnerId"]);
            return Results.Ok(new SessionResponse(
                user.Identity?.IsAuthenticated == true,
                user.FindFirstValue(ClaimTypes.Email),
                registrationEnabled));
        }).AllowAnonymous();

        endpoints.MapPost("/auth/register", async (HttpContext context, RegisterRequest request,
            IAntiforgery antiforgery, UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn, GatewayIdentityDbContext database) =>
        {
            if (!await ValidateAntiforgeryAsync(context, antiforgery))
                return Results.BadRequest(new { message = "La vérification de sécurité a échoué. Rechargez la page." });
            if (!configuration.GetValue("Identity:AllowRegistration", environment.IsDevelopment()))
                return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["credentials"] = ["L’adresse e-mail et le mot de passe sont obligatoires."]
                });

            var email = request.Email.Trim();
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var hasUsers = await users.Users.AnyAsync();
            Guid ownerId;
            try
            {
                ownerId = AccountOwnerIdResolver.Resolve(hasUsers, configuration["Identity:InitialOwnerId"]);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            var user = new ApplicationUser { UserName = email, Email = email, OwnerId = ownerId };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return Results.ValidationProblem(result.Errors
                    .GroupBy(error => error.Code.Contains("Password", StringComparison.OrdinalIgnoreCase) ? "password" : "account")
                    .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
            await transaction.CommitAsync();
            await signIn.SignInAsync(user, isPersistent: false);
            return Results.Ok(new SessionResponse(true, user.Email, true));
        }).AllowAnonymous().RequireRateLimiting("identity-auth");

        endpoints.MapPost("/auth/login", async (HttpContext context, LoginRequest request,
            IAntiforgery antiforgery, SignInManager<ApplicationUser> signIn) =>
        {
            if (!await ValidateAntiforgeryAsync(context, antiforgery))
                return Results.BadRequest(new { message = "La vérification de sécurité a échoué. Rechargez la page." });
            var result = await signIn.PasswordSignInAsync(request.Email?.Trim() ?? string.Empty,
                request.Password ?? string.Empty, isPersistent: request.RememberMe, lockoutOnFailure: true);
            if (!result.Succeeded)
                return Results.Json(new { message = "Adresse e-mail ou mot de passe invalide." }, statusCode: StatusCodes.Status401Unauthorized);

            var user = await signIn.UserManager.FindByEmailAsync(request.Email!.Trim());
            return Results.Ok(new SessionResponse(true, user?.Email, true));
        }).AllowAnonymous().RequireRateLimiting("identity-auth");

        endpoints.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery,
            SignInManager<ApplicationUser> signIn) =>
        {
            if (!await ValidateAntiforgeryAsync(context, antiforgery))
                return Results.BadRequest(new { message = "La vérification de sécurité a échoué. Rechargez la page." });
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        return endpoints;
    }

    private static async Task<bool> ValidateAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private sealed record AntiforgeryResponse(string RequestToken);
    private sealed record SessionResponse(bool IsAuthenticated, string? Email, bool RegistrationEnabled);
    public sealed record RegisterRequest(string Email, string Password);
    public sealed record LoginRequest(string? Email, string? Password, bool RememberMe = false);
}
