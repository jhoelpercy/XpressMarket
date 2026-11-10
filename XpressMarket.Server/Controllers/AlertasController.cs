using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XpressMarket.Server.Data;
using XpressMarket.Server.Services;
using XpressMarket.Shared.Models;

namespace XpressMarket.Server.Controllers
{
    /// <summary>
    /// Panel de alertas del administrador. Las alertas se calculan siempre con las mismas reglas que usan el
    /// dashboard y el informe gerencial (<see cref="ReglasAlertas"/>), y pueden silenciarse cuando dejan de ser útiles.
    /// </summary>
    [Authorize(Roles = "Administrador")]
    [ApiController]
    [Route("api/[controller]")]
    public class AlertasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AlertasController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/alertas
        [HttpGet]
        public async Task<ActionResult<AlertasResumen>> GetAlertas()
        {
            var ahora = DateTime.Now;
            var hoy = ahora.Date;
            var resumen = new AlertasResumen();

            // ---- Stock: agotados y por debajo del mínimo, separando vigentes de silenciados ----
            var productos = await _context.Productos.AsNoTracking()
                .ConStockBajo()
                .Select(p => new AlertaStock
                {
                    ProductoId = p.Id,
                    Nombre = p.Nombre,
                    Categoria = p.Categoria != null ? p.Categoria.Nombre : null,
                    StockActual = p.StockActual,
                    StockMinimo = p.StockMinimo,
                    SilenciadaHasta = p.AlertaStockSilenciadaHasta
                })
                .ToListAsync();

            foreach (var a in productos)
            {
                if (a.SilenciadaHasta != null && a.SilenciadaHasta > ahora)
                    resumen.StockSilenciado.Add(a);
                else
                {
                    a.SilenciadaHasta = null; // un silencio vencido ya no cuenta
                    (a.Agotado ? resumen.Agotados : resumen.StockBajo).Add(a);
                }
            }

            resumen.Agotados = resumen.Agotados.OrderBy(a => a.Nombre).ToList();
            resumen.StockBajo = resumen.StockBajo.OrderByDescending(a => a.Faltante).ThenBy(a => a.Nombre).ToList();
            resumen.StockSilenciado = resumen.StockSilenciado.OrderBy(a => a.Nombre).ToList();

            // ---- Lotes: vencidos y próximos a vencer (sólo con unidades) ----
            var limite = hoy.AddDays(ReglasAlertas.DiasPorVencer + 1);
            var lotes = await _context.Lotes.AsNoTracking()
                .Where(l => l.Activo && l.CantidadActual > 0 && l.FechaVencimiento < limite)
                .Select(l => new
                {
                    l.Id,
                    l.NumeroLote,
                    Producto = l.Producto!.Nombre,
                    l.FechaVencimiento,
                    l.CantidadActual,
                    l.PrecioCosto,
                    l.AlertaVencimientoSilenciadaHasta
                })
                .ToListAsync();

            foreach (var l in lotes.OrderBy(l => l.FechaVencimiento).ThenBy(l => l.Id))
            {
                var alerta = new AlertaLote
                {
                    LoteId = l.Id,
                    NumeroLote = l.NumeroLote,
                    Producto = l.Producto,
                    FechaVencimiento = l.FechaVencimiento,
                    DiasParaVencer = (l.FechaVencimiento.Date - hoy).Days,
                    Cantidad = l.CantidadActual,
                    ValorCosto = l.CantidadActual * l.PrecioCosto,
                    SilenciadaHasta = l.AlertaVencimientoSilenciadaHasta
                };

                if (l.FechaVencimiento.Date < hoy)
                {
                    alerta.SilenciadaHasta = null; // los vencidos no se silencian: se dan de baja como merma
                    resumen.LotesVencidos.Add(alerta);
                }
                else if (alerta.SilenciadaHasta != null && alerta.SilenciadaHasta > ahora)
                    resumen.LotesSilenciados.Add(alerta);
                else
                {
                    alerta.SilenciadaHasta = null;
                    resumen.LotesPorVencer.Add(alerta);
                }
            }

            return resumen;
        }

        // PUT: api/alertas/stock/5/silenciar?dias=7   (dias=0: hasta que se registre un nuevo lote)
        [HttpPut("stock/{productoId}/silenciar")]
        public async Task<IActionResult> SilenciarStock(int productoId, [FromQuery] int dias = 0)
        {
            var producto = await _context.Productos.FindAsync(productoId);
            if (producto == null || !producto.Activo)
                return NotFound("Producto no encontrado.");

            if (producto.StockActual > producto.StockMinimo)
                return BadRequest("Este producto no tiene una alerta de stock bajo.");

            dias = Math.Clamp(dias, 0, 365);
            producto.AlertaStockSilenciadaHasta = dias == 0 ? ReglasAlertas.HastaReponer : DateTime.Now.AddDays(dias);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PUT: api/alertas/stock/5/reactivar
        [HttpPut("stock/{productoId}/reactivar")]
        public async Task<IActionResult> ReactivarStock(int productoId)
        {
            var producto = await _context.Productos.FindAsync(productoId);
            if (producto == null)
                return NotFound("Producto no encontrado.");

            producto.AlertaStockSilenciadaHasta = null;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PUT: api/alertas/lotes/5/silenciar?dias=7   (dias=0: hasta su fecha de vencimiento)
        [HttpPut("lotes/{loteId}/silenciar")]
        public async Task<IActionResult> SilenciarLote(int loteId, [FromQuery] int dias = 0)
        {
            var lote = await _context.Lotes.FindAsync(loteId);
            if (lote == null || !lote.Activo)
                return NotFound("Lote no encontrado.");

            if (lote.Vencido)
                return BadRequest("Un lote vencido no se puede silenciar: regístralo como merma.");

            dias = Math.Clamp(dias, 0, 365);
            lote.AlertaVencimientoSilenciadaHasta = dias == 0
                ? lote.FechaVencimiento.Date.AddDays(1)
                : DateTime.Now.AddDays(dias);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PUT: api/alertas/lotes/5/reactivar
        [HttpPut("lotes/{loteId}/reactivar")]
        public async Task<IActionResult> ReactivarLote(int loteId)
        {
            var lote = await _context.Lotes.FindAsync(loteId);
            if (lote == null)
                return NotFound("Lote no encontrado.");

            lote.AlertaVencimientoSilenciadaHasta = null;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
