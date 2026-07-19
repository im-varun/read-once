using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using ReadOnce.Models.Auth;
using ReadOnce.Services;
using ReadOnce.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services
    .AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Issuer),
        "Jwt:Issuer is required.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Audience),
        "Jwt:Audience is required.")
    .Validate(
        settings => Encoding.UTF8.GetByteCount(settings.SigningKey) >= 32,
        "Jwt:SigningKey must be at least 32 bytes.")
    .Validate(
        settings => settings.ExpiryMinutes > 0,
        "Jwt:ExpiryMinutes must be greater than zero.")
    .ValidateOnStart();

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>() ?? new JwtSettings();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            NameClaimType = JwtRegisteredClaimNames.UniqueName,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis")!;
    var options = ConfigurationOptions.Parse(connectionString);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});
builder.Services.AddScoped<ISecretService, SecretService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddSingleton<IPasswordHasher<UserService>, PasswordHasher<UserService>>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

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

app.MapAuthEndpoints();
app.MapSecretEndpoints();

app.Run();
