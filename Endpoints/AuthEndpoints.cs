using ReadOnce.Models.Auth;
using ReadOnce.Services;

namespace ReadOnce.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/register", async (RegisterRequest request, IUserService userService) =>
        {
            var result = await userService.RegisterAsync(request.Username, request.Password);
            if (!result.Succeeded)
            {
                return Results.Problem(
                    detail: result.Error,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid registration"
                );
            }

            return Results.Created("/auth/login", new { message = "User registered successfully." });
        });

        app.MapPost("/auth/login", async (LoginRequest request, IUserService userService, ITokenService tokenService) =>
        {
            var result = await userService.ValidateCredentialsAsync(request.Username, request.Password);

            if (!result.IsValid || result.UserId is null)
            {
                return Results.Problem(
                    detail: "The username or password is invalid.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid credentials"
                );
            }

            var token = tokenService.GenerateToken(result.UserId, request.Username.Trim());
            return Results.Ok(new AuthResponse(token));
        });
    }
}
