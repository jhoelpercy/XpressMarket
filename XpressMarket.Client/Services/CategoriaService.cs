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
    }
}
