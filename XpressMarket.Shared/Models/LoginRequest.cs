using System;
using System.Collections.Generic;
using System.Text;

namespace XpressMarket.Shared.Models
{
    public class LoginRequest
    {
        public string NombreUsuario { get; set; } = string.Empty;
        public string Contrasena { get; set; } = string.Empty;
    }
}