using System;
using System.Collections.Generic;
using System.Text;

namespace XpressMarket.Shared.Models
{
    public class LoteUpdateRequest
    {
        public Lote Lote { get; set; } = new();
        public bool ActualizarCostoProducto { get; set; } = true;
        public decimal? NuevoPrecioVenta { get; set; }
    }
}