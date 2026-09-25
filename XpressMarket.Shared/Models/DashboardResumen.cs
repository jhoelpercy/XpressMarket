namespace XpressMarket.Shared.Models
{
    public class VentaPorDia
    {
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
    }

    public class ProductoMasVendido
    {
        public string NombreProducto { get; set; } = string.Empty;
        public int CantidadVendida { get; set; }
    }

    public class DashboardResumen
    {
        public decimal TotalVentasPeriodo { get; set; }
        public decimal TotalUtilidadPeriodo { get; set; }
        public int ProductosStockBajo { get; set; }
        public int LotesPorVencer { get; set; }
        public List<VentaPorDia> VentasPorDia { get; set; } = new();
        public List<ProductoMasVendido> TopProductos { get; set; } = new();
    }
}