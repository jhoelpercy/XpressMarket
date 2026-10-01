using System;
using System.Collections.Generic;
using System.Text;
namespace XpressMarket.Shared.Models
{
    public class PeriodoResumen
    {
        public string Etiqueta { get; set; } = string.Empty;
        public DateTime FechaOrden { get; set; }
        public decimal TotalVentas { get; set; }
        public decimal CostoTotal { get; set; }
        public decimal Utilidad { get; set; }
    }
}