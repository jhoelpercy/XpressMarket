using Microsoft.EntityFrameworkCore;
using XpressMarket.Server.Data;
using XpressMarket.Shared.Models;

namespace XpressMarket.Server.Services
{
    /// <summary>
    /// Reglas de alerta compartidas por el panel de alertas, el dashboard, el inicio del administrador
    /// y el informe gerencial, para que todos cuenten exactamente lo mismo.
    /// </summary>
    public static class ReglasAlertas
    {
        /// <summary>Días que cuenta un lote como "próximo a vencer".</summary>
        public const int DiasPorVencer = 30;

        /// <summary>Fecha usada para "silenciar hasta que se registre un nuevo lote".</summary>
        public static readonly DateTime HastaReponer = new(9999, 12, 31);

        /// <summary>Producto activo con stock igual o inferior al mínimo (incluye los silenciados).</summary>
        public static IQueryable<Producto> ConStockBajo(this IQueryable<Producto> q) =>
            q.Where(p => p.Activo && p.StockActual <= p.StockMinimo);

        /// <summary>Alerta de stock bajo vigente: con stock bajo y no silenciada.</summary>
        public static IQueryable<Producto> ConAlertaStockVigente(this IQueryable<Producto> q, DateTime ahora) =>
            q.ConStockBajo().Where(p => p.AlertaStockSilenciadaHasta == null || p.AlertaStockSilenciadaHasta <= ahora);

        public static IQueryable<Producto> ConAlertaStockSilenciada(this IQueryable<Producto> q, DateTime ahora) =>
            q.ConStockBajo().Where(p => p.AlertaStockSilenciadaHasta != null && p.AlertaStockSilenciadaHasta > ahora);

        /// <summary>Lote activo con unidades que vence dentro de los próximos 30 días (incluye los silenciados).</summary>
        public static IQueryable<Lote> PorVencer(this IQueryable<Lote> q, DateTime hoy)
        {
            var limite = hoy.AddDays(DiasPorVencer + 1); // exclusivo: incluye todo el día 30
            return q.Where(l => l.Activo && l.CantidadActual > 0 && l.FechaVencimiento >= hoy && l.FechaVencimiento < limite);
        }

        public static IQueryable<Lote> PorVencerVigentes(this IQueryable<Lote> q, DateTime hoy, DateTime ahora) =>
            q.PorVencer(hoy).Where(l => l.AlertaVencimientoSilenciadaHasta == null || l.AlertaVencimientoSilenciadaHasta <= ahora);

        public static IQueryable<Lote> PorVencerSilenciados(this IQueryable<Lote> q, DateTime hoy, DateTime ahora) =>
            q.PorVencer(hoy).Where(l => l.AlertaVencimientoSilenciadaHasta != null && l.AlertaVencimientoSilenciadaHasta > ahora);

        /// <summary>Lote activo con unidades cuya fecha de vencimiento ya pasó (pendiente de merma).</summary>
        public static IQueryable<Lote> Vencidos(this IQueryable<Lote> q, DateTime hoy) =>
            q.Where(l => l.Activo && l.CantidadActual > 0 && l.FechaVencimiento < hoy);
    }

    /// <summary>
    /// Bloqueo de filas de productos para que dos operaciones simultáneas (por ejemplo, dos cajeros vendiendo
    /// la última unidad) no se pisen. Debe usarse dentro de una transacción y ANTES de leer lotes o stock.
    /// </summary>
    public static class BloqueoStock
    {
        public static async Task BloquearAsync(AppDbContext context, IEnumerable<int> productoIds)
        {
            // Siempre en el mismo orden (por Id) para evitar interbloqueos entre transacciones.
            foreach (var id in productoIds.Distinct().OrderBy(i => i))
            {
                await context.Productos
                    .FromSqlInterpolated($"SELECT * FROM Productos WHERE Id = {id} FOR UPDATE")
                    .ToListAsync();
            }
        }
    }

    public static class ErroresBaseDeDatos
    {
        /// <summary>Indica si la excepción se debe a un índice único (código MySQL 1062).</summary>
        public static bool EsDuplicado(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
                if (e.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
