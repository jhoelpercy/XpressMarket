using System;
using System.Collections.Generic;
using System.Text;

namespace XpressMarket.Shared.Models
{
    public class AdvertenciaAnulacion
    {
        public string NombreProducto { get; set; } = string.Empty;
        public string NumeroLote { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public string Motivo { get; set; } = string.Empty;
    }
}
