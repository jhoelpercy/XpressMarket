using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using XpressMarket.Server.Data;
using XpressMarket.Server.Services;
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

            // El cajero ve su comprobante, pero no el costo de compra de los productos.
            if (!esAdmin)
                foreach (var d in venta.Detalles)
                    if (d.Producto != null) d.Producto.PrecioCosto = 0;

            return venta;
        }

        [HttpPost]
        [Authorize(Roles = "Cajero,Administrador")]
        public async Task<ActionResult<Venta>> PostVenta(Venta venta)
        {
            venta.Id = 0;
            venta.UsuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";
            venta.NombreUsuario = User.FindFirst(ClaimTypes.Name)?.Value ?? "Desconocido";
            venta.Descuento = 0;

            if (venta.Detalles == null || !venta.Detalles.Any())
                return BadRequest("La venta debe tener al menos un detalle.");

            // Lo que llega del cliente nunca decide identificadores, costos ni productos anidados.
            foreach (var d in venta.Detalles)
            {
                d.Id = 0;
                d.VentaId = 0;
                d.Producto = null;
                d.CostoUnitario = 0;
            }

            using var transaccion = await _context.Database.BeginTransactionAsync();

            try
            {
                // Se bloquean las filas de los productos vendidos: si dos cajeros venden la última unidad
                // al mismo tiempo, el segundo espera y recibe "stock insuficiente" en lugar de dejar el stock negativo.
                await BloqueoStock.BloquearAsync(_context, venta.Detalles.Select(d => d.ProductoId));

                var hoy = DateTime.Now.Date;
                decimal subtotalVenta = 0;
                var consumosNuevos = new List<ConsumoLote>();

                foreach (var detalle in venta.Detalles)
                {
                    var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                    if (producto == null || !producto.Activo)
                        return BadRequest($"El producto con ID {detalle.ProductoId} no existe o está desactivado.");

                    // FEFO: primero el lote que vence antes. Los lotes vencidos NO se venden: deben darse de baja como merma.
                    var lotesDisponibles = await _context.Lotes
                        .Where(l => l.ProductoId == detalle.ProductoId && l.Activo && l.CantidadActual > 0
                                    && l.FechaVencimiento >= hoy)
                        .OrderBy(l => l.FechaVencimiento).ThenBy(l => l.Id)
                        .ToListAsync();

                    var vendible = lotesDisponibles.Sum(l => l.CantidadActual);
                    if (vendible < detalle.Cantidad)
                    {
                        var vencidas = Math.Max(0, producto.StockActual - vendible);
                        return BadRequest(vencidas > 0 && producto.StockActual >= detalle.Cantidad
                            ? $"'{producto.Nombre}' tiene {vendible} unidad(es) vendible(s); otras {vencidas} están vencidas y no pueden venderse (regístralas como merma). Solicitado: {detalle.Cantidad}."
                            : $"Stock insuficiente para '{producto.Nombre}'. Disponible: {vendible}, solicitado: {detalle.Cantidad}.");
                    }

                    // Valores reales desde la base de datos
                    detalle.NombreProducto = producto.Nombre;
                    detalle.PrecioUnitario = producto.PrecioVenta;
                    detalle.Descuento = 0;

                    int cantidadPorDescontar = detalle.Cantidad;
                    decimal costoConsumido = 0;

                    foreach (var lote in lotesDisponibles)
                    {
                        if (cantidadPorDescontar <= 0) break;

                        int descontarDeEsteLote = Math.Min(lote.CantidadActual, cantidadPorDescontar);
                        lote.CantidadActual -= descontarDeEsteLote;
                        cantidadPorDescontar -= descontarDeEsteLote;
                        costoConsumido += descontarDeEsteLote * lote.PrecioCosto;

                        consumosNuevos.Add(new ConsumoLote
                        {
                            DetalleVenta = detalle,
                            Lote = lote,
                            Cantidad = descontarDeEsteLote
                        });
                    }

                    // Costo real de esta venta (promedio de los lotes consumidos): las utilidades históricas no cambian después.
                    detalle.CostoUnitario = Math.Round(costoConsumido / detalle.Cantidad, 4);
                    producto.StockActual -= detalle.Cantidad;
                    subtotalVenta += detalle.Cantidad * detalle.PrecioUnitario;
                }

                venta.Subtotal = subtotalVenta;
                venta.Total = subtotalVenta - venta.Descuento;
                venta.FechaVenta = DateTime.Now;
                venta.Estado = EstadoVenta.Completada;

                _context.Ventas.Add(venta);
                _context.Consumos.AddRange(consumosNuevos);
                await _context.SaveChangesAsync();
                await transaccion.CommitAsync();

                // La respuesta no incluye los productos (llevarían el costo de compra al cajero).
                foreach (var d in venta.Detalles)
                    d.Producto = null;

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
            var idsProductos = await _context.DetallesVenta.AsNoTracking()
                .Where(d => d.VentaId == id).Select(d => d.ProductoId).Distinct().ToListAsync();

            using var transaccion = await _context.Database.BeginTransactionAsync();
            try
            {
                // Mismos bloqueos que al vender: una anulación y una venta simultáneas no se pisan el stock,
                // y dos anulaciones de la misma venta no pueden devolver las unidades dos veces.
                await BloqueoStock.BloquearAsync(_context, idsProductos);
                await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Ventas WHERE Id = {id} FOR UPDATE");

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

                // Sólo vuelven al stock las unidades que regresan a un lote activo. Las de un lote dado de baja
                // o en merma ya salieron del inventario y no se reponen (si no, el stock quedaría mayor que la suma de lotes).
                foreach (var detalle in venta.Detalles)
                {
                    var consumosDetalle = consumos.Where(c => c.DetalleVentaId == detalle.Id).ToList();
                    int devueltas = 0;

                    foreach (var consumo in consumosDetalle.Where(c => c.Lote != null && c.Lote.Activo))
                    {
                        consumo.Lote!.CantidadActual += consumo.Cantidad;
                        devueltas += consumo.Cantidad;
                    }

                    // Ventas anteriores al registro de consumos: se devuelve la cantidad completa al producto.
                    if (!consumosDetalle.Any())
                        devueltas = detalle.Cantidad;

                    if (detalle.Producto != null)
                        detalle.Producto.StockActual += devueltas;
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
                    CostoTotal = Math.Round(v.Detalles.Sum(d => d.Cantidad * d.CostoUnitario), 2),
                    Utilidad = Math.Round(v.Total - v.Detalles.Sum(d => d.Cantidad * d.CostoUnitario), 2)
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
            var costoTotal = await detalles.SumAsync(d => (decimal?)(d.Cantidad * d.CostoUnitario)) ?? 0m;

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

            // Sólo se cuentan las alertas vigentes: las que el administrador silenció no inflan el indicador.
            var ahora = DateTime.Now;
            var stockBajoCount = await _context.Productos.ConAlertaStockVigente(ahora).CountAsync();
            var lotesPorVencer = await _context.Lotes.PorVencerVigentes(hoy, ahora).CountAsync();

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
                    Costo = v.Detalles.Sum(d => d.Cantidad * d.CostoUnitario)
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