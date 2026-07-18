using StackExchange.Redis;
using ReadOnce.Services;
using ReadOnce.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis")!;
    var options = ConfigurationOptions.Parse(connectionString);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});
builder.Services.AddScoped<ISecretService, SecretService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () =>
{
    return "Hello, World!";
});

app.MapGet("/health/redis", async (IConnectionMultiplexer redis) =>
{
    try
    {
        var db = redis.GetDatabase();
        var pong = await db.PingAsync();
        return Results.Ok(new
        {
            status = "connected",
            latencyMs = pong.TotalMilliseconds
        });
    }
    catch (RedisConnectionException ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Redis is not available"
        );
    }
});

app.MapPost("/secrets", async (CreateSecretRequest request, ISecretService secretService) =>
{
    var response = await secretService.CreateSecretAsync(request);
    return Results.Created($"/secrets/{response.Id}", response);
});

app.Run();
