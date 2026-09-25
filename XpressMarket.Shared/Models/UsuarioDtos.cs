using System.ComponentModel.DataAnnotations;

namespace XpressMarket.Shared.Models
{
    public class UsuarioResponse
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }

    public class UsuarioCreateRequest
    {
        [Required, StringLength(50, MinimumLength = 3)]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6)]
        public string Contrasena { get; set; } = string.Empty;

        [Required]
        public string Rol { get; set; } = "Cajero";
    }

    public class UsuarioUpdateRequest
    {
        [Required]
        public string Rol { get; set; } = string.Empty;

        [Required]
        public bool Activo { get; set; }

        // Opcional: solo si se quiere resetear la contraseña
        public string? NuevaContrasena { get; set; }
    }
}