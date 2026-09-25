using System.Net.Http.Json;
using XpressMarket.Shared.Models;


namespace XpressMarket.Client.Services
{
    public class UsuarioService
    {
        private readonly HttpClient _http;
        private const string RutaBase = "api/usuarios";

        public UsuarioService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<UsuarioResponse>> ObtenerTodosAsync()
        {
            return await _http.GetFromJsonAsync<List<UsuarioResponse>>(RutaBase) ?? new List<UsuarioResponse>();
        }

        public async Task<(bool Exito, string? Error)> CrearAsync(UsuarioCreateRequest request)
        {
            var respuesta = await _http.PostAsJsonAsync(RutaBase, request);
            if (!respuesta.IsSuccessStatusCode)
                return (false, await respuesta.Content.ReadAsStringAsync());

            return (true, null);
        }

        public async Task<(bool Exito, string? Error)> ActualizarAsync(int id, UsuarioUpdateRequest request)
        {
            var respuesta = await _http.PutAsJsonAsync($"{RutaBase}/{id}", request);
            if (!respuesta.IsSuccessStatusCode)
                return (false, await respuesta.Content.ReadAsStringAsync());

            return (true, null);
        }
    }
}