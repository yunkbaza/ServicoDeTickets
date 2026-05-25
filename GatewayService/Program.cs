using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// A mesma chave exata que está no seu IdentityService
var jwtSecret = "BazaTicketSuperSecretKeyForJwtAuthentication2026";
var key = Encoding.UTF8.GetBytes(jwtSecret);

// 1. Configura o Cadeado (JWT)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // ⚠️ CRUCIAL: Permite rodar em http://localhost
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero // Expira o token no segundo exato
        };
    });

// 2. Cria a política que o YARP (appsettings.json) exige para a Rota de Reservas
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireLoggedIn", policy => policy.RequireAuthenticatedUser());
});

// 3. CORS Seguro
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy => 
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Injeta o YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors("AllowFrontend");

// ⚠️ A ORDEM É SAGRADA NO .NET: Primeiro Autentica, depois Autoriza, depois Roteia!
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "🛡️ BazaTicket API Gateway (YARP) Operacional e Seguro.");

// Entrega o tráfego para os microsserviços (Catálogo, Identidade, Reservas)
app.MapReverseProxy();

// Garante que o Gateway rode sempre na porta 5130
app.Run("http://localhost:5130");