using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using XpressMarket.Server.Data;
using XpressMarket.Shared.Models;

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

        // GET: api/ventas/paginado?pagina=1&tamano=20&desde=2026-10-01&hasta=2026-10-13&estado=1
        // Historial paginado en MySQL. No incluye los detalles, que la tabla no necesita.
        [HttpGet("paginado")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<PagedResult<Venta>>> GetVentasPaginado(
            [FromQuery] int pagina = 1, [FromQuery] int tamano = 20,
            [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null, [FromQuery] int? estado = null)
        {
            tamano = Math.Clamp(tamano, 1, 100);
            pagina = Math.Max(1, pagina);

            var query = _context.Ventas.AsNoTracking().AsQueryable();
            if (desde.HasValue) query = query.Where(v => v.FechaVenta >= desde.Value.Date);
            if (hasta.HasValue) query = query.Where(v => v.FechaVenta < hasta.Value.Date.AddDays(1));
            if (estado.HasValue) query = query.Where(v => (int)v.Estado == estado.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(v => v.FechaVenta).ThenByDescending(v => v.Id)
                .Skip((pagina - 1) * tamano).Take(tamano)
                .ToListAsync();

            return new PagedResult<Venta> { Items = items, Total = total, Pagina = pagina, Tamano = tamano };
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Venta>> GetVenta(int id)
        {
            var venta = await _context.Ventas
                .AsNoTracking()
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (venta == null)
                return NotFound();

            var esAdmin = User.IsInRole("Administrador");
            var idActual = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!esAdmin && venta.UsuarioId != idActual)
                return Forbid();

            return venta;
        }

        [HttpPost]
        [Authorize(Roles = "Cajero,Administrador")]
        public async Task<ActionResult<Venta>> PostVenta(Venta venta)
        {
            venta.UsuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";
            venta.NombreUsuario = User.FindFirst(ClaimTypes.Name)?.Value ?? "Desconocido";
            venta.Descuento = 0;

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

                    // Asignación unificada de valores reales desde la base de datos
                    detalle.NombreProducto = producto.Nombre;
                    detalle.PrecioUnitario = producto.PrecioVenta;
                    detalle.Descuento = 0;

                    subtotalVenta += detalle.Cantidad * detalle.PrecioUnitario;
                    producto.StockActual -= detalle.Cantidad;

                    // Descuento de Lotes FIFO
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

                    if (cantidadPorDescontar > 0)
                    {
                        return BadRequest($"No hay suficientes lotes activos para cubrir la cantidad solicitada de '{producto.Nombre}'.");
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
                .AsNoTracking()
                .Include(v => v.Detalles)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (venta == null)
                return NotFound("Venta no encontrada.");

            if (!User.IsInRole("Administrador") &&
                venta.UsuarioId != User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
                return Forbid();

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
            var query = _context.Ventas.AsNoTracking()
                .Where(v => v.Estado == EstadoVenta.Completada);

            if (desde.HasValue)
                query = query.Where(v => v.FechaVenta >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(v => v.FechaVenta <= hasta.Value.Date.AddDays(1).AddTicks(-1));

            // El costo de cada venta se suma en MySQL; no se cargan ventas, detalles ni productos.
            return await query
                .OrderByDescending(v => v.FechaVenta)
                .Select(v => new ResumenUtilidad
                {
                    VentaId = v.Id,
                    FechaVenta = v.FechaVenta,
                    NombreUsuario = v.NombreUsuario,
                    TotalVenta = v.Total,
                    CostoTotal = v.Detalles.Sum(d => d.Cantidad * d.Producto!.PrecioCosto),
                    Utilidad = v.Total - v.Detalles.Sum(d => d.Cantidad * d.Producto!.PrecioCosto)
                })
                .ToListAsync();
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<DashboardResumen>> GetDashboard([FromQuery] int dias = 30)
        {
            var desde = DateTime.Now.Date.AddDays(-dias);
            var hoy = DateTime.Now.Date;

            var ventas = _context.Ventas.AsNoTracking()
                .Where(v => v.Estado == EstadoVenta.Completada && v.FechaVenta >= desde);
            var detalles = _context.DetallesVenta.AsNoTracking()
                .Where(d => d.Venta!.Estado == EstadoVenta.Completada && d.Venta.FechaVenta >= desde);

            // Todos los indicadores se calculan con agregaciones en MySQL.
            var totalVentas = await ventas.SumAsync(v => (decimal?)v.Total) ?? 0m;
            var costoTotal = await detalles.SumAsync(d => (decimal?)(d.Cantidad * d.Producto!.PrecioCosto)) ?? 0m;

            var ventasPorDia = await ventas
                .GroupBy(v => v.FechaVenta.Date)
                .Select(g => new VentaPorDia { Fecha = g.Key, Total = g.Sum(v => v.Total) })
                .OrderBy(v => v.Fecha)
                .ToListAsync();

            var topProductos = await detalles
                .GroupBy(d => d.NombreProducto)
                .Select(g => new ProductoMasVendido { NombreProducto = g.Key, CantidadVendida = g.Sum(d => d.Cantidad) })
                .OrderByDescending(p => p.CantidadVendida)
                .Take(5)
                .ToListAsync();

            var stockBajoCount = await _context.Productos
                .CountAsync(p => p.Activo && p.StockActual <= p.StockMinimo);

            var lotesPorVencer = await _context.Lotes
                .CountAsync(l => l.Activo && l.CantidadActual > 0
                       && l.FechaVencimiento >= hoy && l.FechaVencimiento < hoy.AddDays(31));

            return new DashboardResumen
            {
                TotalVentasPeriodo = totalVentas,
                TotalUtilidadPeriodo = totalVentas - costoTotal,
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
            var inicio = desde.Date;
            var fin = hasta.Date.AddDays(1); // exclusivo

            var ventas = await _context.Ventas.AsNoTracking()
                .Where(v => v.Estado == EstadoVenta.Completada && v.FechaVenta >= inicio && v.FechaVenta < fin)
                .Select(v => new
                {
                    v.FechaVenta,
                    v.Total,
                    Costo = v.Detalles.Sum(d => d.Cantidad * d.Producto!.PrecioCosto)
                })
                .ToListAsync();

            var cultura = CultureInfo.GetCultureInfo("es-ES");

            (DateTime orden, string etiqueta) ClaveDe(DateTime fecha) => agrupacion.ToLower() switch
            {
                "semana" => ObtenerClaveSemana(fecha),
                "mes" => (new DateTime(fecha.Year, fecha.Month, 1),
                          Capitalizar(cultura.DateTimeFormat.GetMonthName(fecha.Month)) + $" {fecha.Year}"),
                _ => (fecha.Date, fecha.ToString("dd/MM/yyyy"))
            };

            var resumen = ventas
                .GroupBy(v => ClaveDe(v.FechaVenta))
                .Select(g =>
                {
                    var costoTotal = g.Sum(v => v.Costo);
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