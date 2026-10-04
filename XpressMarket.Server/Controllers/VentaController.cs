using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XpressMarket.Server.Data;
using XpressMarket.Shared.Models;
using System.Globalization;

namespace XpressMarket.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VentasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VentasController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<IEnumerable<Venta>>> GetVentas()
        {
            return await _context.Ventas
                .Include(v => v.Detalles)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<Venta>> GetVenta(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (venta == null)
                return NotFound();

            return venta;
        }

        [HttpPost]
        public async Task<ActionResult<Venta>> PostVenta(Venta venta)
        {
            if (venta.Detalles == null || !venta.Detalles.Any())
                return BadRequest("La venta debe tener al menos un detalle.");

            using var transaccion = await _context.Database.BeginTransactionAsync();

            try
            {
                decimal subtotalVenta = 0;
                var consumosNuevos = new List<ConsumoLote>();

                foreach (var detalle in venta.Detalles)
                {
                    var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                    if (producto == null)
                        return BadRequest($"El producto con ID {detalle.ProductoId} no existe.");

                    if (producto.StockActual < detalle.Cantidad)
                        return BadRequest($"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.StockActual}, solicitado: {detalle.Cantidad}.");

                    detalle.NombreProducto = producto.Nombre;
                    if (detalle.PrecioUnitario == 0)
                        detalle.PrecioUnitario = producto.PrecioVenta;

                    subtotalVenta += (detalle.Cantidad * detalle.PrecioUnitario) - detalle.Descuento;

                    producto.StockActual -= detalle.Cantidad;

                    int cantidadPorDescontar = detalle.Cantidad;
                    var lotesDisponibles = await _context.Lotes
                        .Where(l => l.ProductoId == detalle.ProductoId && l.Activo && l.CantidadActual > 0)
                        .OrderBy(l => l.FechaVencimiento)
                        .ToListAsync();

                    foreach (var lote in lotesDisponibles)
                    {
                        if (cantidadPorDescontar <= 0) break;

                        int descontarDeEsteLote = Math.Min(lote.CantidadActual, cantidadPorDescontar);
                        lote.CantidadActual -= descontarDeEsteLote;
                        cantidadPorDescontar -= descontarDeEsteLote;

                        consumosNuevos.Add(new ConsumoLote
                        {
                            DetalleVenta = detalle,
                            Lote = lote,
                            Cantidad = descontarDeEsteLote
                        });
                    }
                }

                venta.Subtotal = subtotalVenta;
                venta.Total = subtotalVenta - venta.Descuento;
                venta.FechaVenta = DateTime.Now;
                venta.Estado = EstadoVenta.Completada;

                _context.Ventas.Add(venta);
                _context.Consumos.AddRange(consumosNuevos);
                await _context.SaveChangesAsync();
                await transaccion.CommitAsync();

                return CreatedAtAction(nameof(GetVenta), new { id = venta.Id }, venta);
            }
            catch
            {
                await transaccion.RollbackAsync();
                throw;
            }
        }

        [HttpPut("{id}/anular")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> AnularVenta(int id, [FromQuery] bool forzar = false)
        {
            var venta = await _context.Ventas.Include(v => v.Detalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.Id == id);
            if (venta == null)
                return NotFound();

            if (venta.Estado == EstadoVenta.Anulada)
                return BadRequest("La venta ya está anulada.");

            var detalleIds = venta.Detalles.Select(d => d.Id).ToList();
            var consumos = await _context.Consumos
                .Include(c => c.Lote)
                .Where(c => detalleIds.Contains(c.DetalleVentaId))
                .ToListAsync();

            var advertencias = consumos
                .Where(c => c.Lote != null && !c.Lote.Activo)
                .Select(c => new AdvertenciaAnulacion
                {
                    NombreProducto = venta.Detalles.First(d => d.Id == c.DetalleVentaId).NombreProducto,
                    NumeroLote = c.Lote!.NumeroLote,
                    Cantidad = c.Cantidad,
                    Motivo = c.Lote.EsMerma
                        ? "Este lote ya fue registrado como merma (se dio por perdido)."
                        : "Este lote ya fue eliminado del inventario."
                })
                .ToList();

            if (advertencias.Any() && !forzar)
                return StatusCode(409, advertencias);

            using var transaccion = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var consumo in consumos.Where(c => c.Lote != null && c.Lote.Activo))
                {
                    consumo.Lote!.CantidadActual += consumo.Cantidad;
                }

                foreach (var detalle in venta.Detalles)
                {
                    if (detalle.Producto != null)
                        detalle.Producto.StockActual += detalle.Cantidad;
                }

                venta.Estado = EstadoVenta.Anulada;
                await _context.SaveChangesAsync();
                await transaccion.CommitAsync();

                return NoContent();
            }
            catch
            {
                await transaccion.RollbackAsync();
                throw;
            }
        }

        [HttpGet("{id}/ticket-pdf")]
        [Authorize] 
        public async Task<IActionResult> GetTicketPdf(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (venta == null)
                return NotFound("Venta no encontrada.");

            try
            {
                byte[] pdfBytes = XpressMarket.Server.Services.TicketPdfService.Generar(venta);
                return File(pdfBytes, "application/pdf", $"Ticket_{venta.Id}.pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR EN TICKET PDF]: {ex.Message}");
                return StatusCode(500, "Error interno al generar el ticket PDF.");
            }
        }

        [HttpGet("reporte-utilidades")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<IEnumerable<ResumenUtilidad>>> GetReporteUtilidades(
            [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var query = _context.Ventas
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(v => v.Estado == EstadoVenta.Completada);

            if (desde.HasValue)
                query = query.Where(v => v.FechaVenta >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(v => v.FechaVenta <= hasta.Value.Date.AddDays(1).AddTicks(-1));

            var ventas = await query.OrderByDescending(v => v.FechaVenta).ToListAsync();

            var resumen = ventas.Select(v =>
            {
                var costoTotal = v.Detalles.Sum(d => d.Cantidad * (d.Producto?.PrecioCosto ?? 0));
                return new ResumenUtilidad
                {
                    VentaId = v.Id,
                    FechaVenta = v.FechaVenta,
                    NombreUsuario = v.NombreUsuario,
                    TotalVenta = v.Total,
                    CostoTotal = costoTotal,
                    Utilidad = v.Total - costoTotal
                };
            }).ToList();

            return resumen;
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<DashboardResumen>> GetDashboard([FromQuery] int dias = 30)
        {
            var desde = DateTime.Now.Date.AddDays(-dias);

            var ventas = await _context.Ventas
                .Include(v => v.Detalles)
                .Where(v => v.Estado == EstadoVenta.Completada && v.FechaVenta >= desde)
                .ToListAsync();

            var productos = await _context.Productos.ToDictionaryAsync(p => p.Id, p => p.PrecioCosto);

            var totalVentas = ventas.Sum(v => v.Total);
            var totalUtilidad = ventas.Sum(v =>
                v.Total - v.Detalles.Sum(d => d.Cantidad * (productos.GetValueOrDefault(d.ProductoId, 0))));

            var ventasPorDia = ventas
                .GroupBy(v => v.FechaVenta.Date)
                .Select(g => new VentaPorDia { Fecha = g.Key, Total = g.Sum(v => v.Total) })
                .OrderBy(v => v.Fecha)
                .ToList();

            var topProductos = ventas
                .SelectMany(v => v.Detalles)
                .GroupBy(d => d.NombreProducto)
                .Select(g => new ProductoMasVendido { NombreProducto = g.Key, CantidadVendida = g.Sum(d => d.Cantidad) })
                .OrderByDescending(p => p.CantidadVendida)
                .Take(5)
                .ToList();

            var stockBajo = await _context.Productos.Where(p => p.Activo).ToListAsync();
            var stockBajoCount = stockBajo.Count(p => p.StockBajo);

            var lotesPorVencer = await _context.Lotes
                .Where(l => l.Activo && l.FechaVencimiento.Date >= DateTime.Now.Date
                       && l.FechaVencimiento.Date <= DateTime.Now.Date.AddDays(30))
                .CountAsync();

            return new DashboardResumen
            {
                TotalVentasPeriodo = totalVentas,
                TotalUtilidadPeriodo = totalUtilidad,
                ProductosStockBajo = stockBajoCount,
                LotesPorVencer = lotesPorVencer,
                VentasPorDia = ventasPorDia,
                TopProductos = topProductos
            };
        }

        [HttpGet("reporte-periodo")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<List<PeriodoResumen>>> GetReportePeriodo(
            [FromQuery] DateTime desde, [FromQuery] DateTime hasta, [FromQuery] string agrupacion = "dia")
        {
            var ventas = await _context.Ventas
                .Include(v => v.Detalles).ThenInclude(d => d.Producto)
                .Where(v => v.Estado == EstadoVenta.Completada
                       && v.FechaVenta.Date >= desde.Date
                       && v.FechaVenta.Date <= hasta.Date)
                .ToListAsync();

            var cultura = CultureInfo.GetCultureInfo("es-ES");

            (DateTime orden, string etiqueta) ClaveDe(Venta v) => agrupacion.ToLower() switch
            {
                "semana" => ObtenerClaveSemana(v.FechaVenta),
                "mes" => (new DateTime(v.FechaVenta.Year, v.FechaVenta.Month, 1),
                          Capitalizar(cultura.DateTimeFormat.GetMonthName(v.FechaVenta.Month)) + $" {v.FechaVenta.Year}"),
                _ => (v.FechaVenta.Date, v.FechaVenta.ToString("dd/MM/yyyy"))
            };

            var resumen = ventas
                .GroupBy(ClaveDe)
                .Select(g =>
                {
                    var costoTotal = g.Sum(v => v.Detalles.Sum(d => d.Cantidad * (d.Producto?.PrecioCosto ?? 0)));
                    var totalVentas = g.Sum(v => v.Total);
                    return new PeriodoResumen
                    {
                        Etiqueta = g.Key.etiqueta,
                        FechaOrden = g.Key.orden,
                        TotalVentas = totalVentas,
                        CostoTotal = costoTotal,
                        Utilidad = totalVentas - costoTotal
                    };
                })
                .OrderBy(r => r.FechaOrden)
                .ToList();

            return resumen;
        }

        private static (DateTime orden, string etiqueta) ObtenerClaveSemana(DateTime fecha)
        {
            var diasDesdeInicioSemana = ((int)fecha.DayOfWeek + 6) % 7;
            var inicioSemana = fecha.Date.AddDays(-diasDesdeInicioSemana);
            var finSemana = inicioSemana.AddDays(6);
            return (inicioSemana, $"{inicioSemana:dd/MM} - {finSemana:dd/MM}");
        }

        private static string Capitalizar(string texto) => char.ToUpper(texto[0]) + texto.Substring(1);
    }
}