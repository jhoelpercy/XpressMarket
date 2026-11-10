using Microsoft.EntityFrameworkCore;
using XpressMarket.Server.Data;
using XpressMarket.Shared.Models;
using QuestPDF.Infrastructure;
using System.Globalization;

System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();
var builder = WebApplication.CreateBuilder(args);

var cultureInfo = new CultureInfo("es-BO");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

QuestPDF.Settings.License = LicenseType.Community;

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var serverVersion = ServerVersion.AutoDetect(connectionString); 

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));


builder.Services.AddControllers();

// Compresión de las respuestas (Brotli/gzip): las listas JSON se reducen de forma notable.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Capturar excepciones no controladas a nivel de proceso
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
{
    Console.WriteLine($"[CRASH INESPERADO DEL PROCESO]: {args.ExceptionObject}");
};

TaskScheduler.UnobservedTaskException += (sender, args) =>
{
    Console.WriteLine($"[CRASH EN TAREA ASÍNCRONA]: {args.Exception}");
    args.SetObserved();
};

builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirClient", policy =>
    {
        policy.WithOrigins("https://localhost:7204", "http://localhost:5217")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var jwtKey = builder.Configuration["Jwt:Key"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(jwtKey))
    };

    // Diagnóstico de 403: deja en consola quién fue rechazado, con qué rol y en qué ruta.
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        // El token no basta: el usuario debe seguir activo y conservar el rol del token. Así, desactivar a un
        // usuario o cambiarle el rol surte efecto de inmediato y no recién cuando vence el token (8 horas).
        OnTokenValidated = async ctx =>
        {
            var idTexto = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var rolToken = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (!int.TryParse(idTexto, out var idUsuario))
            {
                ctx.Fail("Token sin identificador de usuario.");
                return;
            }

            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var actual = await db.Usuarios.AsNoTracking()
                .Where(u => u.Id == idUsuario)
                .Select(u => new { u.Activo, u.Rol })
                .FirstOrDefaultAsync();

            if (actual == null || !actual.Activo || actual.Rol != rolToken)
                ctx.Fail("La sesión ya no es válida: el usuario fue desactivado o cambió de rol.");
        },
        OnForbidden = ctx =>
        {
            var roles = string.Join(",", ctx.HttpContext.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value));
            Console.WriteLine($"[403] {ctx.Request.Method} {ctx.Request.Path} usuario='{ctx.HttpContext.User.Identity?.Name}' roles='{roles}'");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

   

    if (!db.Usuarios.Any(u => u.EsSuperAdmin))
    {
        db.Usuarios.Add(new Usuario
        {
            NombreUsuario = "gerencia",
            NombreCompleto = "Cuenta de Respaldo del Sistema",
            ContrasenaHash = BCrypt.Net.BCrypt.HashPassword("Gerencia#2026!"),
            Rol = "Administrador",
            Activo = true,
            EsSuperAdmin = true
        });
        db.SaveChanges();
    }

    db.SaveChanges();
}

app.UseResponseCompression();

app.UseCors("PermitirClient");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();

app.UseAuthorization();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
