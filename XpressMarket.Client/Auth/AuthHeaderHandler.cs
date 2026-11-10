using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.Authorization;

namespace XpressMarket.Client.Auth
{
    public class AuthHeaderHandler : DelegatingHandler
    {
        private readonly CustomAuthStateProvider _authStateProvider;

        public AuthHeaderHandler(AuthenticationStateProvider authStateProvider)
        {
            _authStateProvider = (CustomAuthStateProvider)authStateProvider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_authStateProvider.Token == null)
                await _authStateProvider.GetAuthenticationStateAsync();

            if (!string.IsNullOrWhiteSpace(_authStateProvider.Token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authStateProvider.Token);

            var respuesta = await base.SendAsync(request, cancellationToken);

            // Token vencido, usuario desactivado o rol cambiado: el servidor responde 401 y la sesión se cierra,
            // con lo que la aplicación vuelve a la pantalla de ingreso en lugar de mostrar errores sueltos.
            if (respuesta.StatusCode == System.Net.HttpStatusCode.Unauthorized && request.Headers.Authorization != null)
                await _authStateProvider.CerrarSesionAsync();

            return respuesta;
        }
    }
}