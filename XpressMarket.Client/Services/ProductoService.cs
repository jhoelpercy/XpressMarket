using System.Net.Http.Json;
using XpressMarket.Shared.Models;

namespace XpressMarket.Client.Services
{
    public class ProductoService
    {
        private readonly HttpClient _http;
        private const string RutaBase = "api/productos";

        public ProductoService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<Producto>> ObtenerTodosAsync()
        {
            return await _http.GetFromJsonAsync<List<Producto>>(RutaBase) ?? new List<Producto>();
        }

        public async Task<Producto?> ObtenerPorIdAsync(int id)
        {
            return await _http.GetFromJsonAsync<Producto>($"{RutaBase}/{id}");
        }

        public async Task<List<Producto>> ObtenerStockBajoAsync()
        {
            return await _http.GetFromJsonAsync<List<Producto>>($"{RutaBase}/stock-bajo") ?? new List<Producto>();
        }

        public async Task<Producto?> CrearAsync(Producto producto)
        {
            var respuesta = await _http.PostAsJsonAsync(RutaBase, producto);
            if (!respuesta.IsSuccessStatusCode)
                return null;

            return await respuesta.Content.ReadFromJsonAsync<Producto>();
        }

        public async Task<bool> ActualizarAsync(int id, Producto producto)
        {
            var respuesta = await _http.PutAsJsonAsync($"{RutaBase}/{id}", producto);
            return respuesta.IsSuccessStatusCode;
        }

        public async Task<bool> EliminarAsync(int id)
        {
            var respuesta = await _http.DeleteAsync($"{RutaBase}/{id}");
            return respuesta.IsSuccessStatusCode;
        }
    }
}