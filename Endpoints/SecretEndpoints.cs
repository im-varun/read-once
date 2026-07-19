using ReadOnce.Models;
using ReadOnce.Services;

namespace ReadOnce.Endpoints;

public static class SecretEndpoints
{
    public static void MapSecretEndpoints(this WebApplication app)
    {
        app.MapPost("/secrets", async (CreateSecretRequest request, ISecretService secretService) =>
        {
            try
            {
                var response = await secretService.CreateSecretAsync(request);
                return Results.Created($"/secrets/{response.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request"
                );
            }
        });

        app.MapGet("/secrets/{id}", async (string id, ISecretService secretService) =>
        {
            var content = await secretService.GetAndDeleteSecretAsync(id);

            return content is null
                ? Results.NotFound(new { message = "Secret not found, expired or has already been retrieved." })
                : Results.Ok(new { content });
        });
    }
}
