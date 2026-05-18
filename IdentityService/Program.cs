using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Configuração básica de CORS (para permitir o Gateway ou o Angular)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors("AllowAll");

// A Chave Secreta do seu BazaTicket (Em produção, isso vai pro appsettings.json)
var secretKey = "BazaTicketSuperSecretKeyForJwtAuthentication2026"; 

// Endpoint de Login
app.MapPost("/api/auth/login", (LoginRequest request) =>
{
    // Validação Mock (Depois trocamos pelo MongoDB/PostgreSQL)
    if (request.Email == "admin@baza.com" && request.Password == "123456")
    {
        // 1. Criar as informações (Claims) do usuário
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "user-id-123"),
            new Claim(JwtRegisteredClaimNames.Email, request.Email),
            new Claim(ClaimTypes.Role, "VIP")
        };

        // 2. Assinar o token com a sua chave secreta
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "BazaTicketIdentity",
            audience: "BazaTicketFrontend",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2), // Token dura 2 horas
            signingCredentials: creds
        );

        // 3. Devolver o Token como texto
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        
        return Results.Ok(new { token = tokenString });
    }

    return Results.Unauthorized();
});

// Força o IdentityService a rodar na porta 5300
app.Run("http://localhost:5300");

// Record para mapear o JSON que vem do Angular
public record LoginRequest(string Email, string Password);