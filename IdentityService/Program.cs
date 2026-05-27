using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// 🛡️ CONEXÃO COM MONGODB REAL
var mongoClient = new MongoClient("mongodb://localhost:27017");
var db = mongoClient.GetDatabase("BazaTicketIdentityDb");
var usersCollection = db.GetCollection<User>("Users");

// Para injeção de dependência nas rotas
builder.Services.AddSingleton(usersCollection);

var app = builder.Build();
app.UseCors("AllowAll");

var secretKey = "BazaTicketSuperSecretKeyForJwtAuthentication2026"; 

// 🔥 ROTA 1: REGISTRO DE USUÁRIO REAL
app.MapPost("/api/auth/register", async (RegisterRequest request, IMongoCollection<User> users) =>
{
    // Verifica se o e-mail já existe
    var existingUser = await users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
    if (existingUser != null) return Results.BadRequest(new { message = "E-mail já cadastrado." });

    // Cria o usuário com Hash Seguro (Nunca salve senhas em texto puro!)
    var newUser = new User
    {
        Name = request.Name,
        Email = request.Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        Role = "User"
    };

    await users.InsertOneAsync(newUser);
    return Results.Ok(new { message = "Conta criada com sucesso." });
});

// 🔥 ROTA 2: LOGIN REAL VALIDADO NO BANCO
app.MapPost("/api/auth/login", async (LoginRequest request, IMongoCollection<User> users) =>
{
    var user = await users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
    
    // Valida se usuário existe e se a senha bate com o Hash
    if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id), // O ID real do MongoDB
        new Claim(JwtRegisteredClaimNames.Name, user.Name),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role)
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: "BazaTicketIdentity",
        audience: "BazaTicketFrontend",
        claims: claims,
        expires: DateTime.UtcNow.AddDays(7), // Token dura 7 dias
        signingCredentials: creds
    );

    return Results.Ok(new 
    { 
        token = new JwtSecurityTokenHandler().WriteToken(token),
        user = new { id = user.Id, name = user.Name, email = user.Email }
    });
});

app.Run("http://localhost:5300");

// --- MODELS DE DOMÍNIO ---
public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}

public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);