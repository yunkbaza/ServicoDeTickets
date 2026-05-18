using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = "BazaTicketSuperSecretKeyForJwtAuthentication2026";
var key = Encoding.UTF8.GetBytes(jwtSecret);

// 1. Configura o Cadeado (JWT)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// 2. Cria a política "RequireLoggedIn" que o appsettings.json está procurando
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireLoggedIn", policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors("AllowFrontend");

// Ativa a segurança
app.UseAuthentication();
app.UseAuthorization();

// Rota de Login para gerar o Token
app.MapPost("/api/login", (LoginRequest request) =>
{
    if (request.Email == "admin@dropship.com" && request.Password == "123456")
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, request.Email) }),
            Expires = DateTime.UtcNow.AddHours(2),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Results.Ok(new { Token = tokenHandler.WriteToken(token) });
    }
    return Results.Unauthorized();
});

app.MapGet("/", () => "🔒 API Gateway Seguro (YARP) está online.");

app.MapReverseProxy();

app.Run();

public record LoginRequest(string Email, string Password);