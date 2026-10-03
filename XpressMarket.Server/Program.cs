using Microsoft.EntityFrameworkCore;
using XpressMarket.Server.Data;
using XpressMarket.Shared.Models;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))));

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirClient", policy =>
    {
        policy.WithOrigins("https://localhost:7204")
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
});

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.Categorias.Any())
    {
        var abarrotes = new Categoria { Nombre = "Abarrotes", Descripcion = "Productos de despensa", Activo = true };
        var bebidas = new Categoria { Nombre = "Bebidas", Descripcion = "Gaseosas y jugos", Activo = true };
        var lacteos = new Categoria { Nombre = "Lácteos", Descripcion = "Leche, yogurt, quesos", Activo = true };
        var limpieza = new Categoria { Nombre = "Limpieza", Descripcion = "Artículos de aseo", Activo = true };
        db.Categorias.AddRange(abarrotes, bebidas, lacteos, limpieza);

        var pil = new Proveedor { Nombre = "PIL Andina S.A.", Contacto = "Ventas PIL", Telefono = "77712345", Activo = true };
        var cocaCola = new Proveedor { Nombre = "Coca-Cola Bolivia", Contacto = "Distribución", Telefono = "77754321", Activo = true };
        var laFrancesa = new Proveedor { Nombre = "Distribuidora La Francesa", Contacto = "Pedidos", Telefono = "77798765", Activo = true };
        db.Proveedores.AddRange(pil, cocaCola, laFrancesa);

        db.SaveChanges(); // para obtener los Id generados antes de usarlos abajo

        var productos = new List<(Producto p, Proveedor prov, int cantVenceProto, int cantVenceLargo)>
        {
            (new Producto { Nombre = "Leche Pil 1L", Descripcion = "Leche entera", CategoriaId = lacteos.Id,
                PrecioCosto = 6.00m, PrecioVenta = 8.50m, StockMinimo = 10, CodigoBarras = "7791234560001", Activo = true }, pil, 15, 20),

            (new Producto { Nombre = "Galletas Oreo 108g", Descripcion = "Galletas rellenas", CategoriaId = abarrotes.Id,
                PrecioCosto = 3.00m, PrecioVenta = 4.50m, StockMinimo = 15, CodigoBarras = "7791234560002", Activo = true }, laFrancesa, 20, 25),

            (new Producto { Nombre = "Coca-Cola 2L", Descripcion = "Gaseosa", CategoriaId = bebidas.Id,
                PrecioCosto = 7.00m, PrecioVenta = 10.00m, StockMinimo = 12, CodigoBarras = "7791234560003", Activo = true }, cocaCola, 18, 24),

            (new Producto { Nombre = "Detergente Ace 1kg", Descripcion = "Detergente en polvo", CategoriaId = limpieza.Id,
                PrecioCosto = 12.00m, PrecioVenta = 16.00m, StockMinimo = 8, CodigoBarras = "7791234560004", Activo = true }, laFrancesa, 10, 15),

            (new Producto { Nombre = "Yogurt Pil 1L", Descripcion = "Yogurt natural", CategoriaId = lacteos.Id,
                PrecioCosto = 5.00m, PrecioVenta = 7.00m, StockMinimo = 10, CodigoBarras = "7791234560005", Activo = true }, pil, 12, 18),

            (new Producto { Nombre = "Arroz Aranjuez 1kg", Descripcion = "Arroz blanco", CategoriaId = abarrotes.Id,
                PrecioCosto = 4.00m, PrecioVenta = 6.00m, StockMinimo = 20, CodigoBarras = "7791234560006", Activo = true }, laFrancesa, 30, 40),
        };

        foreach (var (producto, proveedor, cantCorto, cantLargo) in productos)
        {
            db.Productos.Add(producto);
        }
        db.SaveChanges();

        foreach (var (producto, proveedor, cantCorto, cantLargo) in productos)
        {
            // Lote A: vence pronto (10 días) — para ver la alerta de "próximo a vencer"
            var loteA = new Lote
            {
                NumeroLote = $"{producto.Nombre.Split(' ')[0].ToUpper()}-A",
                ProductoId = producto.Id,
                ProveedorId = proveedor.Id,
                FechaIngreso = DateTime.Now.AddDays(-20),
                FechaVencimiento = DateTime.Now.AddDays(10),
                CantidadInicial = cantCorto,
                CantidadActual = cantCorto,
                PrecioCosto = producto.PrecioCosto,
                Activo = true
            };

            // Lote B: vence lejos (5 meses) — stock "normal"
            var loteB = new Lote
            {
                NumeroLote = $"{producto.Nombre.Split(' ')[0].ToUpper()}-B",
                ProductoId = producto.Id,
                ProveedorId = proveedor.Id,
                FechaIngreso = DateTime.Now.AddDays(-2),
                FechaVencimiento = DateTime.Now.AddMonths(5),
                CantidadInicial = cantLargo,
                CantidadActual = cantLargo,
                PrecioCosto = producto.PrecioCosto,
                Activo = true
            };

            db.Lotes.AddRange(loteA, loteB);

            producto.StockActual = cantCorto + cantLargo;
        }

        var pan = new Producto
        {
            Nombre = "Pan de Molde Ideal",
            Descripcion = "Pan de molde integral",
            CategoriaId = abarrotes.Id,
            PrecioCosto = 8.00m,
            PrecioVenta = 11.00m,
            StockMinimo = 5,
            CodigoBarras = "7791234560007",
            Activo = true
        };
        db.Productos.Add(pan);
        db.SaveChanges();

        var lotePanVencido = new Lote
        {
            NumeroLote = "PAN-VENCIDO",
            ProductoId = pan.Id,
            FechaIngreso = DateTime.Now.AddDays(-30),
            FechaVencimiento = DateTime.Now.AddDays(-5), // ya vencido
            CantidadInicial = 8,
            CantidadActual = 8,
            PrecioCosto = pan.PrecioCosto,
            Activo = true
        };
        db.Lotes.Add(lotePanVencido);
        pan.StockActual = 8;

        db.SaveChanges();
    }

    // Crear usuario cajero si no existe, con contraseña hasheada limpia
    var cajero = db.Usuarios.FirstOrDefault(u => u.NombreUsuario == "cajero");
    string hashCajero = BCrypt.Net.BCrypt.HashPassword("cajero123");
    if (cajero == null)
    {
        db.Usuarios.Add(new Usuario
        {
            NombreUsuario = "cajero",
            NombreCompleto = "Juan Pérez",
            ContrasenaHash = hashCajero,
            Rol = "Cajero",
            Activo = true
        });
    }
    else
    {
        cajero.ContrasenaHash = hashCajero;
    }

    // Crear usuario administrador si no existe, con contraseña hasheada limpia
    var admin = db.Usuarios.FirstOrDefault(u => u.NombreUsuario == "admin");
    string hashAdmin = BCrypt.Net.BCrypt.HashPassword("Admin123*");
    if (admin == null)
    {
        db.Usuarios.Add(new Usuario
        {
            NombreUsuario = "admin",
            NombreCompleto = "Administrador del Sistema",
            ContrasenaHash = hashAdmin,
            Rol = "Administrador",
            Activo = true
        });
    }
    else
    {
        admin.ContrasenaHash = hashAdmin;
    }

    db.SaveChanges();
}

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
