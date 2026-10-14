namespace XpressMarket.Client.Services
{
    public class ReporteService
    {
        private readonly HttpClient _http;
        private const string RutaBase = "api/reportes";

        public ReporteService(HttpClient http)
        {
            _http = http;
        }

        public async Task<byte[]> ObtenerInformeGerencialPdfAsync(DateTime desde, DateTime hasta)
        {
            return await _http.GetByteArrayAsync(
                $"api/reportes/informe-gerencial/pdf?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}");
        }
        public async Task<byte[]?> ObtenerMermasExcelAsync(DateTime? desde, DateTime? hasta)
        {
            var url = $"{RutaBase}/mermas/excel?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
            var response = await _http.GetAsync(url);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
        }

        public async Task<byte[]?> ObtenerMermasPdfAsync(DateTime? desde, DateTime? hasta)
        {
            var url = $"{RutaBase}/mermas/pdf?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
            var response = await _http.GetAsync(url);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
        }

        public async Task<byte[]?> ObtenerUtilidadesExcelAsync(DateTime? desde, DateTime? hasta)
        {
            var url = $"{RutaBase}/utilidades/excel?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
            var response = await _http.GetAsync(url);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
        }

        public async Task<byte[]?> ObtenerUtilidadesPdfAsync(DateTime? desde, DateTime? hasta)
        {
            var url = $"{RutaBase}/utilidades/pdf?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
            var response = await _http.GetAsync(url);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
        }
    }
}