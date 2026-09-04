using System.Net.Http.Json;
using XpressMarket.Shared.Models;

namespace XpressMarket.Client.Services
{
    public class LoteService
    {
        private readonly HttpClient _http;
        private const string RutaBase = "api/lotes";

        public LoteService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<Lote>> ObtenerTodosAsync()
        {
            return await _http.GetFromJsonAsync<List<Lote>>(RutaBase) ?? new List<Lote>();
        }

        public async Task<Lote?> ObtenerPorIdAsync(int id)
        {
            return await _http.GetFromJsonAsync<Lote>($"{RutaBase}/{id}");
        }

        public async Task<List<Lote>> ObtenerProximosAVencerAsync()
        {
            return await _http.GetFromJsonAsync<List<Lote>>($"{RutaBase}/proximos-a-vencer") ?? new List<Lote>();
        }

        public async Task<List<Lote>> ObtenerVencidosAsync()
        {
            return await _http.GetFromJsonAsync<List<Lote>>($"{RutaBase}/vencidos") ?? new List<Lote>();
        }

        public async Task<Lote?> CrearAsync(Lote lote)
        {
            var respuesta = await _http.PostAsJsonAsync(RutaBase, lote);
            if (!respuesta.IsSuccessStatusCode)
                return null;

            return await respuesta.Content.ReadFromJsonAsync<Lote>();
        }

        public async Task<bool> ActualizarAsync(int id, Lote lote)
        {
            var respuesta = await _http.PutAsJsonAsync($"{RutaBase}/{id}", lote);
            return respuesta.IsSuccessStatusCode;
        }

        public async Task<bool> EliminarAsync(int id)
        {
            var respuesta = await _http.DeleteAsync($"{RutaBase}/{id}");
            return respuesta.IsSuccessStatusCode;
        }
    }
}