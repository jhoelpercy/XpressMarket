using System;
using System.Collections.Generic;
using System.Text;

namespace XpressMarket.Shared.Models
{
    public class InformeGerencial
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }

        public int CantidadVentas { get; set; }
        public decimal TotalVentas { get; set; }
        public decimal TotalCosto { get; set; }
        public decimal TotalUtilidad { get; set; }

        public int CantidadMermas { get; set; }
        public decimal ValorMermas { get; set; }

        public List<ProductoResumen> ProductosStockBajo { get; set; } = new();
        public List<LoteResumen> LotesVencidos { get; set; } = new();
        public List<LoteResumen> LotesProximosAVencer { get; set; } = new();
        public List<ProductoMasVendido> TopProductos { get; set; } = new();
    }

    public class ProductoResumen
    {
        public string Nombre { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
    }

    public class LoteResumen
    {
        public string NumeroLote { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public DateTime FechaVencimiento { get; set; }
        public int Cantidad { get; set; }
    }
}