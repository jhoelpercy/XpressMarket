namespace XpressMarket.Shared.Models
{
    /// <summary>Página de resultados devuelta por los endpoints paginados.</summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Pagina { get; set; }
        public int Tamano { get; set; }
    }

    /// <summary>Conteos del inventario para el inicio del administrador (se calculan en la base de datos).</summary>
    public class ResumenInventario
    {
        public int TotalProductos { get; set; }
        public int StockBajo { get; set; }
        public int LotesPorVencer { get; set; }
        public int LotesVencidos { get; set; }
    }
}
