using System.Net.Http.Json;
using XpressMarket.Shared.Models;

namespace XpressMarket.Client.Services
{
    public class CategoriaService
    {
        private readonly HttpClient _http;
        private const string RutaBase = "api/categorias";

        public CategoriaService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<Categoria>> ObtenerTodasAsync()
        {

            return await _http.GetFromJsonAsync<List<Categoria>>(RutaBase) ?? new List<Categoria>();
        }
        public async Task<Categoria?> CrearAsync(Categoria categoria)
        {
            var respuesta = await _http.PostAsJsonAsync(RutaBase, categoria);
            if (!respuesta.IsSuccessStatusCode)
                return null;

            return await respuesta.Content.ReadFromJsonAsync<Categoria>();
        }

        public async Task<bool> ActualizarAsync(int id, Categoria categoria)
        {
            var respuesta = await _http.PutAsJsonAsync($"{RutaBase}/{id}", categoria);
            return respuesta.IsSuccessStatusCode;
        }

        public async Task<bool> EliminarAsync(int id)
        {
            var respuesta = await _http.DeleteAsync($"{RutaBase}/{id}");
            return respuesta.IsSuccessStatusCode;
        }
    }
}
