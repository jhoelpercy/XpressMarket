using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using XpressMarket.Client;
using XpressMarket.Client.Services; // Asegúrate de incluir este namespace si ahí viven tus servicios

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// 1. Configuración ÚNICA de HttpClient apuntando a la Web API (Server)
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7232/") 
});

// 2. Registro de Servicios de Negocio
builder.Services.AddScoped<ProductoService>();
builder.Services.AddScoped<ProveedorService>();
builder.Services.AddScoped<LoteService>();
builder.Services.AddScoped<CategoriaService>();
await builder.Build().RunAsync();