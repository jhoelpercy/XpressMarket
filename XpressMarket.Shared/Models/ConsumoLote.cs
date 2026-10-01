using System;
using System.Collections.Generic;
using System.Text;

namespace XpressMarket.Shared.Models
{
    public class ConsumoLote
    {
        public int Id { get; set; }
        public int DetalleVentaId { get; set; }
        public DetalleVenta? DetalleVenta { get; set; }
        public int LoteId { get; set; }
        public Lote? Lote { get; set; }
        public int Cantidad { get; set; }
    }
}