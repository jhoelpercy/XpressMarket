using System;
using System.Collections.Generic;
using System.Text;

using System.ComponentModel.DataAnnotations;

namespace XpressMarket.Shared.Models
{
    public class Usuario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string NombreUsuario { get; set; } = string.Empty;

        [StringLength(100)]
        public string? NombreCompleto { get; set; }

        [Required]
        public string ContrasenaHash { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Rol { get; set; } = "Cajero"; // "Administrador" o "Cajero"

        [Required]
        public bool Activo { get; set; } = true;

        public bool EsSuperAdmin { get; set; } = false;
    }
}   