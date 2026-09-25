using System.Net.Http.Json;
using XpressMarket.Shared.Models;

namespace XpressMarket.Client.Services
{
    public class VentaService
    {
        private readonly HttpClient _http;
        private const string RutaBase = "api/ventas";

        public VentaService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<Venta>> ObtenerTodasAsync()
        {
            return await _http.GetFromJsonAsync<List<Venta>>(RutaBase) ?? new List<Venta>();
        }

        public async Task<(bool Exito, string? Error, Venta? Venta)> RegistrarVentaAsync(Venta venta)
        {
            var respuesta = await _http.PostAsJsonAsync(RutaBase, venta);

            if (!respuesta.IsSuccessStatusCode)
            {
                var error = await respuesta.Content.ReadAsStringAsync();
                return (false, error, null);
            }

            var ventaCreada = await respuesta.Content.ReadFromJsonAsync<Venta>();
            return (true, null, ventaCreada);
        }

        public async Task<bool> AnularVentaAsync(int id)
        {
            var respuesta = await _http.PutAsync($"{RutaBase}/{id}/anular", null);
            return respuesta.IsSuccessStatusCode;
        }

        public async Task<byte[]> ObtenerComprobantePdfAsync(int ventaId)
        {
            var respuesta = await _http.GetAsync($"{RutaBase}/{ventaId}/comprobante");

            if (respuesta.IsSuccessStatusCode)
            {
                return await respuesta.Content.ReadAsByteArrayAsync();
            }

            return Array.Empty<byte>();
        }

        public async Task<List<ResumenUtilidad>> ObtenerReporteUtilidadesAsync(DateTime? desde = null, DateTime? hasta = null)
        {
            var filtros = new List<string>();
            if (desde.HasValue) filtros.Add($"desde={desde:yyyy-MM-dd}");
            if (hasta.HasValue) filtros.Add($"hasta={hasta:yyyy-MM-dd}");
            var queryString = filtros.Any() ? "?" + string.Join("&", filtros) : "";

            return await _http.GetFromJsonAsync<List<ResumenUtilidad>>($"{RutaBase}/reporte-utilidades{queryString}") ?? new List<ResumenUtilidad>();
        }
        public async Task<DashboardResumen?> ObtenerDashboardAsync(int dias = 30)
        {
            return await _http.GetFromJsonAsync<DashboardResumen>($"{RutaBase}/dashboard?dias={dias}");
        }
    }
}