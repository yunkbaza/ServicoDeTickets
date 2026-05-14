var builder = WebApplication.CreateBuilder(args);

// 1. Adiciona o serviço do YARP e manda ele ler as regras do appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapGet("/", () => "🌐 API Gateway (YARP) está online. Roteando o tráfego da Dropship Hub!");

// 2. Liga a turbina do Proxy
app.MapReverseProxy();

app.Run();