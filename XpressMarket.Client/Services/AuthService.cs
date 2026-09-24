using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using XpressMarket.Client.Auth;
using XpressMarket.Shared.Models;

namespace XpressMarket.Client.Services
{
    public class AuthService
    {
        private readonly HttpClient _http;
        private readonly CustomAuthStateProvider _authStateProvider;
        private readonly IJSRuntime _js;

        public AuthService(HttpClient http, AuthenticationStateProvider authStateProvider, IJSRuntime js)
        {
            _http = http;
            _authStateProvider = (CustomAuthStateProvider)authStateProvider;
            _js = js;
        }

        public async Task<(bool Exito, string? Error)> LoginAsync(string nombreUsuario, string contrasena)
        {
            var respuesta = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest
            {
                NombreUsuario = nombreUsuario,
                Contrasena = contrasena
            });

            if (!respuesta.IsSuccessStatusCode)
            {
                var error = await respuesta.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(error) ? "Usuario o contraseña incorrectos." : error);
            }

            var resultado = await respuesta.Content.ReadFromJsonAsync<LoginResponse>();
            if (resultado == null)
                return (false, "Respuesta inválida del servidor.");

            await _js.InvokeVoidAsync("localStorageSet", "authToken", resultado.Token);
            _authStateProvider.MarcarComoAutenticado(resultado.Token);

            return (true, null);
        }

        public async Task LogoutAsync()
        {
            await _js.InvokeVoidAsync("localStorageRemove", "authToken");
            _authStateProvider.MarcarComoDesconectado();
        }
    }
}