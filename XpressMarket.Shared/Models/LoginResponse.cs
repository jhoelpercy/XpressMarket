using System;
using System.Collections.Generic;
using System.Text;
namespace XpressMarket.Shared.Models
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string NombreUsuario { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
    }
}