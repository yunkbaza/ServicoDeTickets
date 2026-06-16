using System.Text;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. CONFIGURAÇÃO DO CADEADO (JWT)
// ==========================================
var jwtSecret = "BazaTicketSuperSecretKeyForJwtAuthentication2026";
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // Permite rodar em http://localhost
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero // Expira o token no segundo exato
        };
        
        // 🔥 MÁGICA SÊNIOR: O SignalR envia o Token JWT via QueryString (URL) em vez do Header.
        // Precisamos ensinar o Gateway a pescar esse token para permitir WebSockets seguros!
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/ticket"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// 2. Cria a política que o YARP (appsettings.json) exige
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireLoggedIn", policy => policy.RequireAuthenticatedUser());
});

// ==========================================
// 3. CORS SEGURO (Atualizado para o SignalR)
// ==========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // ⚠️ CRÍTICO: Sem isso, o SignalR (WebSocket) dá erro de CORS!
    });
});

// ==========================================
// 4. YARP REVERSE PROXY
// ==========================================
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ==========================================
// 5. SIGNALR (Tempo Real)
// ==========================================
builder.Services.AddSignalR();

// ==========================================
// 6. MASSTRANSIT & RABBITMQ (Ouvindo o Mercado)
// ==========================================
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<InventoryUpdatedEventConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => { h.Username("guest"); h.Password("guest"); });
        
        cfg.ReceiveEndpoint("gateway-signalr-queue", e =>
        {
            e.ConfigureConsumer<InventoryUpdatedEventConsumer>(context);
        });
    });
});

var app = builder.Build();

app.UseCors("AllowFrontend");

// ⚠️ A ORDEM É SAGRADA NO .NET: Primeiro Autentica, depois Autoriza, depois Roteia!
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "🛡️ BazaTicket API Gateway (YARP) Operacional, Seguro e em Tempo Real.");

// Mapeia o "Tubo" de WebSockets do SignalR
app.MapHub<TicketHub>("/hubs/ticket");

// Entrega o tráfego HTTP normal para os microsserviços (Catálogo, Identidade, Reservas)
app.MapReverseProxy();

app.Run("http://localhost:5130");

// ==========================================
// CLASSES DE INFRAESTRUTURA (SIGNALR + RABBITMQ)
// ==========================================
public class TicketHub : Hub { }

public record InventoryUpdatedEvent(string EventId, int AvailableTickets);

public class InventoryUpdatedEventConsumer : IConsumer<InventoryUpdatedEvent>
{
    private readonly IHubContext<TicketHub> _hubContext;
    
    public InventoryUpdatedEventConsumer(IHubContext<TicketHub> hubContext) 
    {
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<InventoryUpdatedEvent> context)
    {
        // Pega a mensagem do RabbitMQ e empurra instantaneamente para todos os Frontends conectados!
        await _hubContext.Clients.All.SendAsync("UpdateStock", context.Message.EventId, context.Message.AvailableTickets);
    }
}