using System.ComponentModel.DataAnnotations;

namespace XpressMarket.Shared.Models
{
    public class UsuarioResponse
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string NombreUsuario { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }

    public class UsuarioCreateRequest
    {
        [StringLength(100, ErrorMessage = "El nombre completo no puede superar los 100 caracteres.")]
        public string? NombreCompleto { get; set; }

        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "El nombre de usuario debe tener entre 3 y 50 caracteres.")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Contrasena { get; set; } = string.Empty;

        [Required(ErrorMessage = "El rol es obligatorio.")]
        public string Rol { get; set; } = "Cajero";
    }

    public class UsuarioUpdateRequest
    {
        [Required(ErrorMessage = "El rol es obligatorio.")]
        public string Rol { get; set; } = "Cajero";

        [Required]
        public bool Activo { get; set; }

        // MinLength solo valida si el campo NO es nulo ni está vacío
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string? NuevaContrasena { get; set; }
    }
}