using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using XpressMarket.Client;
using XpressMarket.Client.Auth;
using XpressMarket.Client.Services;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);


var cultureInfo = new CultureInfo("es-BO");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;


builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// 1. Registro del DelegatingHandler para inyectar el token JWT en las peticiones
builder.Services.AddScoped<AuthHeaderHandler>();

// 2. Configuración del HttpClient autenticado apuntando a la Web API (Server)
builder.Services.AddHttpClient("XpressMarketAPI", client =>
{
    client.BaseAddress = new Uri("https://localhost:7232/"); // Reemplaza por la URL de tu API
}).AddHttpMessageHandler<AuthHeaderHandler>();

// 3. Registrar HttpClient genérico para que use la instancia autenticada por defecto
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("XpressMarketAPI"));

// 4. Servicios de Autenticación y Autorización
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddScoped<AuthService>();

//servicios de MudBlazor
builder.Services.AddMudServices();

// 5. Servicios de Negocio
builder.Services.AddScoped<ProductoService>();
builder.Services.AddScoped<ProveedorService>();
builder.Services.AddScoped<LoteService>();
builder.Services.AddScoped<CategoriaService>();
builder.Services.AddScoped<VentaService>();
builder.Services.AddScoped<UsuarioService>();

// 6. Construir y ejecutar la aplicación al FINAL de la configuración
await builder.Build().RunAsync();