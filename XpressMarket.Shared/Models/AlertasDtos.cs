namespace XpressMarket.Shared.Models
{
    /// <summary>Alerta de stock de un producto (agotado o por debajo del mínimo).</summary>
    public class AlertaStock
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Categoria { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public int Faltante => Math.Max(0, StockMinimo - StockActual);
        public bool Agotado => StockActual <= 0;
        public DateTime? SilenciadaHasta { get; set; }
    }

    /// <summary>Alerta de vencimiento de un lote (próximo a vencer o vencido).</summary>
    public class AlertaLote
    {
        public int LoteId { get; set; }
        public string NumeroLote { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public DateTime FechaVencimiento { get; set; }
        public int DiasParaVencer { get; set; }
        public int Cantidad { get; set; }
        public decimal ValorCosto { get; set; }
        public DateTime? SilenciadaHasta { get; set; }
    }

    /// <summary>Todas las alertas vigentes y las silenciadas, calculadas en el servidor.</summary>
    public class AlertasResumen
    {
        public List<AlertaStock> Agotados { get; set; } = new();
        public List<AlertaStock> StockBajo { get; set; } = new();
        public List<AlertaLote> LotesVencidos { get; set; } = new();
        public List<AlertaLote> LotesPorVencer { get; set; } = new();
        public List<AlertaStock> StockSilenciado { get; set; } = new();
        public List<AlertaLote> LotesSilenciados { get; set; } = new();

        public int TotalVigentes => Agotados.Count + StockBajo.Count + LotesVencidos.Count + LotesPorVencer.Count;
        public int TotalSilenciadas => StockSilenciado.Count + LotesSilenciados.Count;
        public decimal ValorVencido => LotesVencidos.Sum(l => l.ValorCosto);
    }
}
