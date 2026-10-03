namespace XpressMarket.Client.Services
{
    public class ReporteService
    {
        private readonly HttpClient _http;

        public ReporteService(HttpClient http)
        {
            _http = http;
        }

        public async Task<byte[]> ObtenerInformeGerencialPdfAsync(DateTime desde, DateTime hasta)
        {
            return await _http.GetByteArrayAsync(
                $"api/reportes/informe-gerencial/pdf?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}");
        }
    }
}