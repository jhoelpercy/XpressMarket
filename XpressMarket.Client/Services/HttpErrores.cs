using System.Text.Json;

namespace XpressMarket.Client.Services
{
    public static class HttpErrores
    {
        public static async Task<string> LeerAsync(HttpResponseMessage respuesta)
        {
            var texto = await respuesta.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(texto))
                return $"Error {(int)respuesta.StatusCode}";

            try
            {
                using var doc = JsonDocument.Parse(texto);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("errors", out var errores))
                {
                    var mensajes = errores.EnumerateObject()
                        .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()))
                        .Where(m => !string.IsNullOrWhiteSpace(m));
                    return string.Join(" ", mensajes);
                }
                if (doc.RootElement.ValueKind == JsonValueKind.String)
                    return doc.RootElement.GetString() ?? texto;
            }
            catch (JsonException) { }

            return texto;
        }
    }
}