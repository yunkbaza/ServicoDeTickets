# ETAPA 1: Construção (Build)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copia apenas o arquivo de configuração e restaura os pacotes (Otimização de cache)
COPY ["GatewayService.csproj", "./"]
RUN dotnet restore "GatewayService.csproj"

# Copia o resto do código e compila
COPY . .
RUN dotnet publish "GatewayService.csproj" -c Release -o /out

# ETAPA 2: Produção (Imagem leve apenas com o runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .

# Expõe a porta 8080 (padrão do Docker para .NET 8+)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Comando para ligar o serviço
ENTRYPOINT ["dotnet", "GatewayService.dll"]