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

        // Página de productos filtrada y ordenada en el servidor (administrador).
        public async Task<PagedResult<Producto>> ObtenerPaginadoAsync(int pagina, int tamano, string? q = null, int? categoriaId = null)
        {
            var url = $"{RutaBase}/paginado?pagina={pagina}&tamano={tamano}";
            if (!string.IsNullOrWhiteSpace(q)) url += $"&q={Uri.EscapeDataString(q.Trim())}";
            if (categoriaId.HasValue) url += $"&categoriaId={categoriaId.Value}";
            return await _http.GetFromJsonAsync<PagedResult<Producto>>(url) ?? new PagedResult<Producto>();
        }

        // Mejores coincidencias para el buscador del punto de venta (sólo trae unas pocas filas).
        public async Task<List<Producto>> BuscarAsync(string? q, int max = 20, CancellationToken ct = default)
        {
            var url = $"{RutaBase}/buscar?max={max}";
            if (!string.IsNullOrWhiteSpace(q)) url += $"&q={Uri.EscapeDataString(q.Trim())}";
            return await _http.GetFromJsonAsync<List<Producto>>(url, ct) ?? new List<Producto>();
        }

        // Conteos del inicio del administrador, calculados en la base de datos.
        public async Task<ResumenInventario> ObtenerResumenInventarioAsync()
        {
            return await _http.GetFromJsonAsync<ResumenInventario>($"{RutaBase}/resumen-inventario") ?? new ResumenInventario();
        }

        public async Task<List<Producto>> ObtenerStockBajoAsync()
        {
            return await _http.GetFromJsonAsync<List<Producto>>($"{RutaBase}/stock-bajo") ?? new List<Producto>();
        }

        public string? UltimoError { get; private set; }

        public async Task<Producto?> CrearAsync(Producto producto)
        {
            UltimoError = null;
            var respuesta = await _http.PostAsJsonAsync(RutaBase, producto);
            if (!respuesta.IsSuccessStatusCode)
            {
                UltimoError = await HttpErrores.LeerAsync(respuesta);
                return null;
            }
            return await respuesta.Content.ReadFromJsonAsync<Producto>();
        }

        public async Task<bool> ActualizarAsync(int id, Producto producto)
        {
            UltimoError = null;
            var respuesta = await _http.PutAsJsonAsync($"{RutaBase}/{id}", producto);
            if (!respuesta.IsSuccessStatusCode)
                UltimoError = await HttpErrores.LeerAsync(respuesta);
            return respuesta.IsSuccessStatusCode;
        }

        public async Task<bool> EliminarAsync(int id)
        {
            UltimoError = null;
            var respuesta = await _http.DeleteAsync($"{RutaBase}/{id}");
            if (!respuesta.IsSuccessStatusCode)
                UltimoError = await HttpErrores.LeerAsync(respuesta);
            return respuesta.IsSuccessStatusCode;
        }
    }
}