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

            return await base.SendAsync(request, cancellationToken);
        }
    }
}