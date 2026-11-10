using System.Net.Http.Json;
using XpressMarket.Shared.Models;

namespace XpressMarket.Client.Services
{
    public class AlertaService
    {
        private readonly HttpClient _http;
        private const string RutaBase = "api/alertas";

        public AlertaService(HttpClient http)
        {
            _http = http;
        }

        public string? UltimoError { get; private set; }

        public async Task<AlertasResumen> ObtenerAsync()
            => await _http.GetFromJsonAsync<AlertasResumen>(RutaBase) ?? new AlertasResumen();

        /// <summary>dias = 0: hasta que se registre un nuevo lote del producto.</summary>
        public Task<bool> SilenciarStockAsync(int productoId, int dias)
            => EnviarAsync($"{RutaBase}/stock/{productoId}/silenciar?dias={dias}");

        public Task<bool> ReactivarStockAsync(int productoId)
            => EnviarAsync($"{RutaBase}/stock/{productoId}/reactivar");

        /// <summary>dias = 0: hasta la fecha de vencimiento del lote.</summary>
        public Task<bool> SilenciarLoteAsync(int loteId, int dias)
            => EnviarAsync($"{RutaBase}/lotes/{loteId}/silenciar?dias={dias}");

        public Task<bool> ReactivarLoteAsync(int loteId)
            => EnviarAsync($"{RutaBase}/lotes/{loteId}/reactivar");

        private async Task<bool> EnviarAsync(string url)
        {
            var respuesta = await _http.PutAsync(url, null);
            UltimoError = respuesta.IsSuccessStatusCode ? null : await HttpErrores.LeerAsync(respuesta);
            return respuesta.IsSuccessStatusCode;
        }
    }
}
