using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        public async Task<ActionResult<IEnumerable<Venta>>> GetVentas()
        {
            return await _context.Ventas
                .Include(v => v.Detalles)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Venta>> GetVenta(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Detalles)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (venta == null)
                return NotFound();

            return venta;
        }

        // POST: api/ventas — Registra la venta y descuenta stock de forma transaccional (FEFO)
        [HttpPost]
        public async Task<ActionResult<Venta>> PostVenta(Venta venta)
        {
            if (venta.Detalles == null || !venta.Detalles.Any())
                return BadRequest("La venta debe tener al menos un detalle.");

            using var transaccion = await _context.Database.BeginTransactionAsync();

            try
            {
                decimal subtotalVenta = 0;

                foreach (var detalle in venta.Detalles)
                {
                    var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                    if (producto == null)
                        return BadRequest($"El producto con ID {detalle.ProductoId} no existe.");

                    if (producto.StockActual < detalle.Cantidad)
                        return BadRequest($"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.StockActual}, solicitado: {detalle.Cantidad}.");

                    // Congelamos el nombre y precio actuales en el detalle (historial)
                    detalle.NombreProducto = producto.Nombre;
                    if (detalle.PrecioUnitario == 0)
                        detalle.PrecioUnitario = producto.PrecioVenta;

                    subtotalVenta += (detalle.Cantidad * detalle.PrecioUnitario) - detalle.Descuento;

                    // Descuento de stock general del producto
                    producto.StockActual -= detalle.Cantidad;

                    // Descuento FEFO: primero el lote que vence antes
                    int cantidadPorDescontar = detalle.Cantidad;
                    var lotesDisponibles = await _context.Lotes
                        .Where(l => l.ProductoId == detalle.ProductoId
                                 && l.Activo
                                 && l.CantidadActual > 0)
                        .OrderBy(l => l.FechaVencimiento)
                        .ToListAsync();

                    foreach (var lote in lotesDisponibles)
                    {
                        if (cantidadPorDescontar <= 0) break;

                        int descontarDeEsteLote = Math.Min(lote.CantidadActual, cantidadPorDescontar);
                        lote.CantidadActual -= descontarDeEsteLote;
                        cantidadPorDescontar -= descontarDeEsteLote;
                    }

                    // Nota: si cantidadPorDescontar > 0 aqui, significa que el stock de Producto
                    // estaba desincronizado con la suma real de sus Lotes. Se permite continuar
                    // porque ya validamos stock arriba, pero es una señal para revisar consistencia.
                }

                venta.Subtotal = subtotalVenta;
                venta.Total = subtotalVenta - venta.Descuento;
                venta.FechaVenta = DateTime.Now;
                venta.Estado = EstadoVenta.Completada;

                _context.Ventas.Add(venta);
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

        // PUT: api/ventas/5/anular — anula una venta (no se borra, por trazabilidad)
        [HttpPut("{id}/anular")]
        public async Task<IActionResult> AnularVenta(int id)
        {
            var venta = await _context.Ventas.FindAsync(id);
            if (venta == null)
                return NotFound();

            if (venta.Estado == EstadoVenta.Anulada)
                return BadRequest("La venta ya está anulada.");

            venta.Estado = EstadoVenta.Anulada;
            await _context.SaveChangesAsync();

            return NoContent();
        }
        // GET: api/ventas/5/comprobante
        [HttpGet("{id}/comprobante")]
        public async Task<IActionResult> GetComprobante(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Detalles)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (venta == null)
                return NotFound();

            var pdfBytes = XpressMarket.Server.Services.ComprobantePdfService.Generar(venta);
            return File(pdfBytes, "application/pdf", $"Comprobante_Venta_{venta.Id}.pdf");
        }
        // GET: api/ventas/reporte-utilidades?desde=2026-01-01&hasta=2026-12-31
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
        }// GET: api/ventas/dashboard?dias=30
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

    }
}