using System.IdentityModel.Tokens.Jwt;
using ReadOnce.Models;
using ReadOnce.Services;

namespace ReadOnce.Endpoints;

public static class SecretEndpoints
{
    public static void MapSecretEndpoints(this WebApplication app)
    {
        app.MapPost("/secrets", async (CreateSecretRequest request, HttpContext httpContext, ISecretService secretService) =>
        {
            var userId = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Problem(
                    detail: "The authenticated user id claim is missing.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized"
                );
            }

            var result = await secretService.CreateSecretAsync(request, userId);
            if (!result.Succeeded || result.Response is null)
            {
                return Results.Problem(
                    detail: result.Error,
                    statusCode: result.ErrorType == SecretCreationError.InvalidRequest
                        ? StatusCodes.Status400BadRequest
                        : StatusCodes.Status500InternalServerError,
                    title: result.ErrorType == SecretCreationError.InvalidRequest
                        ? "Invalid request"
                        : "Secret creation failed"
                );
            }

            return Results.Created($"/secrets/{result.Response.Id}", result.Response);
        }).RequireAuthorization();

        app.MapGet("/secrets/{id}", async (string id, ISecretService secretService) =>
        {
            var content = await secretService.GetAndDeleteSecretAsync(id);

            return content is null
                ? Results.NotFound(new { message = "Secret not found, expired or has already been retrieved." })
                : Results.Ok(new { content });
        });

        app.MapGet("/users/me/secrets", async (HttpContext httpContext, ISecretService secretService) =>
        {
            var userId = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Problem(
                    detail: "The authenticated user id claim is missing.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized"
                );
            }

            var secrets = await secretService.GetUserSecretsAsync(userId);
            return Results.Ok(secrets);
        }).RequireAuthorization();
    }
}
