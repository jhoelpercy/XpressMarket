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

        public async Task<bool> CrearAsync(LoteCreateRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/lotes", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ActualizarAsync(int id, LoteUpdateRequest request)
        {
            var respuesta = await _http.PutAsJsonAsync($"{RutaBase}/{id}", request);
            return respuesta.IsSuccessStatusCode;
        }

        public async Task<bool> EliminarAsync(int id)
        {
            var respuesta = await _http.DeleteAsync($"{RutaBase}/{id}");
            return respuesta.IsSuccessStatusCode;
        }
        public async Task<bool> RegistrarMermaAsync(int id)
        {
            var respuesta = await _http.PutAsync($"{RutaBase}/{id}/registrar-merma", null);
            return respuesta.IsSuccessStatusCode;
        }

        public async Task<List<Lote>> ObtenerReporteMermasAsync(DateTime? desde = null, DateTime? hasta = null)
        {
            var filtros = new List<string>();
            if (desde.HasValue) filtros.Add($"desde={desde:yyyy-MM-dd}");
            if (hasta.HasValue) filtros.Add($"hasta={hasta:yyyy-MM-dd}");
            var queryString = filtros.Any() ? "?" + string.Join("&", filtros) : "";

            return await _http.GetFromJsonAsync<List<Lote>>($"{RutaBase}/reporte-mermas{queryString}") ?? new List<Lote>();
        }
        public async Task<List<PeriodoResumen>> ObtenerReporteMermasPeriodoAsync(DateTime desde, DateTime hasta, string agrupacion)
        {
            return await _http.GetFromJsonAsync<List<PeriodoResumen>>(
                $"{RutaBase}/reporte-mermas-periodo?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}&agrupacion={agrupacion}") ?? new();
        }
    }
}