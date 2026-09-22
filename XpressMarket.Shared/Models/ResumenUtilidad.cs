namespace XpressMarket.Shared.Models
{
    public class ResumenUtilidad
    {
        public int VentaId { get; set; }
        public DateTime FechaVenta { get; set; }
        public decimal TotalVenta { get; set; }
        public decimal CostoTotal { get; set; }
        public decimal Utilidad { get; set; }
    }
}
