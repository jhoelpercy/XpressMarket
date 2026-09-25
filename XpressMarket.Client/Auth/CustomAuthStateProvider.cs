using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace XpressMarket.Client.Auth
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _js;
        private string? _token;

        public CustomAuthStateProvider(IJSRuntime js)
        {
            _js = js;
        }

        public string? Token => _token;

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            _token ??= await _js.InvokeAsync<string?>("localStorageGet", "authToken");

            if (string.IsNullOrWhiteSpace(_token))
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            var claims = ParsearClaims(_token);

            // Verifica si el token ya expiró
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
            if (expClaim != null && long.TryParse(expClaim.Value, out var expUnix))
            {
                var expiracion = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                if (expiracion < DateTimeOffset.UtcNow)
                {
                    _token = null;
                    await _js.InvokeVoidAsync("localStorageRemove", "authToken");
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }
            }

            var identidad = new ClaimsIdentity(claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identidad));
        }
        public void MarcarComoAutenticado(string token)
        {
            _token = token;
            var claims = ParsearClaims(token);
            var identidad = new ClaimsIdentity(claims, "jwt");
            var usuario = new ClaimsPrincipal(identidad);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(usuario)));
        }

        public void MarcarComoDesconectado()
        {
            _token = null;
            var usuario = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(usuario)));
        }

        private static List<Claim> ParsearClaims(string jwt)
        {
            var payload = jwt.Split('.')[1];
            var bytesJson = ParseBase64WithoutPadding(payload);
            var claves = JsonSerializer.Deserialize<Dictionary<string, object>>(bytesJson);

            return claves!.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString() ?? "")).ToList();
        }

        private static byte[] ParseBase64WithoutPadding(string base64)
        {
            base64 = base64.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}