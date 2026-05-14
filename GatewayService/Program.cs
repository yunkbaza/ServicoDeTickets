var builder = WebApplication.CreateBuilder(args);

// 1. CONFIGURAÇÃO DE CORS (Essencial para o Frontend Angular conseguir acessar)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()  // Permite qualquer URL (como localhost:4200)
              .AllowAnyMethod()  // Permite GET, POST, PUT, DELETE
              .AllowAnyHeader(); // Permite qualquer cabeçalho
    });
});

// 2. Adiciona o serviço do YARP lendo do appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// 3. ATIVAR O CORS (Tem que vir ANTES do MapReverseProxy)
app.UseCors("AllowFrontend");

app.MapGet("/", () => "🌐 API Gateway (YARP) está online. Roteando o tráfego da Dropship Hub!");

// 4. Liga a turbina do Proxy
app.MapReverseProxy();

app.Run();