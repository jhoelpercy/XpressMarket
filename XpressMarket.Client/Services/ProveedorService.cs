using System.Net.Http.Json;
using XpressMarket.Shared.Models;

namespace XpressMarket.Client.Services
{
    public class ProveedorService
    {
        private readonly HttpClient _http;
        private const string RutaBase = "api/proveedores";

        public ProveedorService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<Proveedor>> ObtenerTodosAsync()
        {
            return await _http.GetFromJsonAsync<List<Proveedor>>(RutaBase) ?? new List<Proveedor>();
        }

        public async Task<Proveedor?> ObtenerPorIdAsync(int id)
        {
            return await _http.GetFromJsonAsync<Proveedor>($"{RutaBase}/{id}");
        }

        public async Task<Proveedor?> CrearAsync(Proveedor proveedor)
        {
            var respuesta = await _http.PostAsJsonAsync(RutaBase, proveedor);
            if (!respuesta.IsSuccessStatusCode)
                return null;

            return await respuesta.Content.ReadFromJsonAsync<Proveedor>();
        }

        public async Task<bool> ActualizarAsync(int id, Proveedor proveedor)
        {
            var respuesta = await _http.PutAsJsonAsync($"{RutaBase}/{id}", proveedor);
            return respuesta.IsSuccessStatusCode;
        }

        public async Task<bool> EliminarAsync(int id)
        {
            var respuesta = await _http.DeleteAsync($"{RutaBase}/{id}");
            return respuesta.IsSuccessStatusCode;
        }
    }
}
