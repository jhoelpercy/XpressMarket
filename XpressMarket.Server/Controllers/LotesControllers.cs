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
    [Authorize(Roles = "Administrador")]
    [ApiController]
    [Route("api/[controller]")]
    public class LotesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LotesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Lote>>> GetLotes()
        {
            return await _context.Lotes
                .AsNoTracking()
                .Include(l => l.Producto)
                .Include(l => l.Proveedor)
                .Where(l => l.Activo)
                .ToListAsync();
        }

        // GET: api/lotes/paginado?pagina=1&tamano=20&q=leche&estado=vencidos
        // estado: todos | vencidos | por-vencer | vigentes. El filtrado y el paginado ocurren en MySQL.
        [HttpGet("paginado")]
        public async Task<ActionResult<PagedResult<Lote>>> GetPaginado(
            [FromQuery] int pagina = 1, [FromQuery] int tamano = 20,
            [FromQuery] string? q = null, [FromQuery] string estado = "todos")
        {
            tamano = Math.Clamp(tamano, 1, 100);
            pagina = Math.Max(1, pagina);
            var hoy = DateTime.Now.Date;

            var query = _context.Lotes.AsNoTracking().Where(l => l.Activo);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                query = query.Where(l => l.NumeroLote.Contains(t) || l.Producto!.Nombre.Contains(t));
            }

            query = estado switch
            {
                "vencidos" => query.Where(l => l.CantidadActual > 0 && l.FechaVencimiento < hoy),
                "por-vencer" => query.Where(l => l.CantidadActual > 0 && l.FechaVencimiento >= hoy && l.FechaVencimiento < hoy.AddDays(31)),
                "vigentes" => query.Where(l => l.FechaVencimiento >= hoy.AddDays(31)),
                _ => query
            };

            var total = await query.CountAsync();
            var items = await query
                .Include(l => l.Producto)
                .Include(l => l.Proveedor)
                .OrderBy(l => l.FechaVencimiento).ThenBy(l => l.Id)
                .Skip((pagina - 1) * tamano).Take(tamano)
                .ToListAsync();

            return new PagedResult<Lote> { Items = items, Total = total, Pagina = pagina, Tamano = tamano };
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Lote>> GetLote(int id)
        {
            var lote = await _context.Lotes
                .Include(l => l.Producto)
                .Include(l => l.Proveedor)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lote == null)
                return NotFound();

            return lote;
        }

        // GET: api/lotes/proximos-a-vencer
        // Alertas vigentes: no incluye los lotes que el administrador silenció (se ven en el panel de alertas).
        [HttpGet("proximos-a-vencer")]
        public async Task<ActionResult<IEnumerable<Lote>>> GetProximosAVencer()
        {
            var ahora = DateTime.Now;
            return await _context.Lotes
                .AsNoTracking()
                .Include(l => l.Producto)
                .PorVencerVigentes(ahora.Date, ahora)
                .OrderBy(l => l.FechaVencimiento)
                .ToListAsync();
        }

        // GET: api/lotes/vencidos
        [HttpGet("vencidos")]
        public async Task<ActionResult<IEnumerable<Lote>>> GetVencidos()
        {
            return await _context.Lotes
                .AsNoTracking()
                .Include(l => l.Producto)
                .Vencidos(DateTime.Now.Date)
                .OrderBy(l => l.FechaVencimiento)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Lote>> PostLote(LoteCreateRequest request)
        {
            var lote = request.Lote;

            // Un lote nuevo siempre nace activo, sin merma, con todas sus unidades disponibles.
            lote.Id = 0;
            lote.Producto = null;
            lote.Proveedor = null;
            lote.NumeroLote = lote.NumeroLote.Trim();
            lote.CantidadActual = lote.CantidadInicial;
            lote.Activo = true;
            lote.EsMerma = false;
            lote.FechaMerma = null;
            lote.AlertaVencimientoSilenciadaHasta = null;
            lote.FechaIngreso = DateTime.Now;

            if (lote.ProveedorId.HasValue && !await _context.Proveedores.AnyAsync(p => p.Id == lote.ProveedorId.Value && p.Activo))
                return BadRequest("El proveedor especificado no existe o está desactivado.");

            lote.RegistradoPorUsuarioId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
            lote.RegistradoPorNombre = User.FindFirst(ClaimTypes.Name)?.Value;

            using var transaccion = await _context.Database.BeginTransactionAsync();
            await BloqueoStock.BloquearAsync(_context, new[] { lote.ProductoId });

            var producto = await _context.Productos.FindAsync(lote.ProductoId);
            if (producto == null || !producto.Activo)
                return BadRequest("El producto especificado no existe o está desactivado.");

            // El stock físico del producto crece con cada lote que entra al inventario.
            producto.StockActual += lote.CantidadActual;

            // Una reposición cierra el silencio de la alerta de stock bajo: si vuelve a bajar, avisa de nuevo.
            producto.AlertaStockSilenciadaHasta = null;

            if (request.ActualizarCostoProducto || producto.PrecioCosto == 0)
                producto.PrecioCosto = lote.PrecioCosto;

            if (request.NuevoPrecioVenta.HasValue && request.NuevoPrecioVenta.Value > 0)
                producto.PrecioVenta = request.NuevoPrecioVenta.Value;

            _context.Lotes.Add(lote);
            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            return CreatedAtAction(nameof(GetLote), new { id = lote.Id }, lote);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutLote(int id, LoteUpdateRequest request)
        {
            var loteNuevo = request.Lote;
            if (id != loteNuevo.Id)
                return BadRequest("El ID de la ruta no coincide con el del lote.");

            var productoId = await _context.Lotes.AsNoTracking()
                .Where(l => l.Id == id).Select(l => (int?)l.ProductoId).FirstOrDefaultAsync();
            if (productoId == null)
                return NotFound();

            using var transaccion = await _context.Database.BeginTransactionAsync();
            await BloqueoStock.BloquearAsync(_context, new[] { productoId.Value });

            var loteExistente = await _context.Lotes.FindAsync(id);
            if (loteExistente == null)
                return NotFound();

            if (loteExistente.EsMerma)
                return BadRequest("Este lote fue registrado como merma y ya no puede modificarse.");

            var producto = await _context.Productos.FindAsync(loteExistente.ProductoId);
            if (producto == null)
                return BadRequest("El producto asociado no existe.");

            // La cantidad inicial es un dato histórico: no se modifica. La actual sólo corrige conteos físicos.
            if (loteNuevo.CantidadActual > loteExistente.CantidadInicial)
                return BadRequest($"La cantidad actual no puede superar la cantidad inicial del lote ({loteExistente.CantidadInicial}).");

            if (loteNuevo.ProveedorId.HasValue && !await _context.Proveedores.AnyAsync(p => p.Id == loteNuevo.ProveedorId.Value))
                return BadRequest("El proveedor especificado no existe.");

            // Sólo un lote activo aporta unidades al stock del producto: se ajusta por la diferencia de aporte
            // (cubre cambios de cantidad y también la activación o desactivación del lote).
            int aporteAnterior = loteExistente.Activo ? loteExistente.CantidadActual : 0;
            int aporteNuevo = loteNuevo.Activo ? loteNuevo.CantidadActual : 0;
            var nuevoStock = producto.StockActual + (aporteNuevo - aporteAnterior);
            if (nuevoStock < 0)
                return BadRequest("El cambio dejaría el stock del producto en negativo.");
            producto.StockActual = nuevoStock;

            if (request.ActualizarCostoProducto)
                producto.PrecioCosto = loteNuevo.PrecioCosto;

            if (request.NuevoPrecioVenta.HasValue && request.NuevoPrecioVenta.Value > 0)
                producto.PrecioVenta = request.NuevoPrecioVenta.Value;

            // Si cambia la fecha de vencimiento, el silencio anterior de la alerta ya no corresponde.
            if (loteExistente.FechaVencimiento.Date != loteNuevo.FechaVencimiento.Date)
                loteExistente.AlertaVencimientoSilenciadaHasta = null;

            loteExistente.NumeroLote = loteNuevo.NumeroLote.Trim();
            loteExistente.ProveedorId = loteNuevo.ProveedorId;
            loteExistente.FechaVencimiento = loteNuevo.FechaVencimiento;
            loteExistente.CantidadActual = loteNuevo.CantidadActual;
            loteExistente.PrecioCosto = loteNuevo.PrecioCosto;
            loteExistente.Activo = loteNuevo.Activo;

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLote(int id)
        {
            var productoId = await _context.Lotes.AsNoTracking()
                .Where(l => l.Id == id).Select(l => (int?)l.ProductoId).FirstOrDefaultAsync();
            if (productoId == null)
                return NotFound();

            using var transaccion = await _context.Database.BeginTransactionAsync();
            await BloqueoStock.BloquearAsync(_context, new[] { productoId.Value });

            var lote = await _context.Lotes.FindAsync(id);
            if (lote == null)
                return NotFound();

            if (lote.Activo && lote.CantidadActual > 0)
            {
                var producto = await _context.Productos.FindAsync(lote.ProductoId);
                if (producto != null)
                    producto.StockActual = Math.Max(0, producto.StockActual - lote.CantidadActual);
            }

            lote.Activo = false;
            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            return NoContent();
        }

        // PUT: api/lotes/5/registrar-merma
        [HttpPut("{id}/registrar-merma")]
        public async Task<IActionResult> RegistrarMerma(int id)
        {
            var productoId = await _context.Lotes.AsNoTracking()
                .Where(l => l.Id == id).Select(l => (int?)l.ProductoId).FirstOrDefaultAsync();
            if (productoId == null)
                return NotFound();

            using var transaccion = await _context.Database.BeginTransactionAsync();
            await BloqueoStock.BloquearAsync(_context, new[] { productoId.Value });

            var lote = await _context.Lotes.FindAsync(id);
            if (lote == null)
                return NotFound();

            if (!lote.Vencido)
                return BadRequest("Solo se pueden registrar como merma los lotes vencidos.");

            if (lote.EsMerma)
                return BadRequest("Este lote ya fue registrado como merma.");

            // Un lote ya dado de baja no vuelve a descontar del stock (ya salió del inventario).
            if (lote.Activo && lote.CantidadActual > 0)
            {
                var producto = await _context.Productos.FindAsync(lote.ProductoId);
                if (producto != null)
                    producto.StockActual = Math.Max(0, producto.StockActual - lote.CantidadActual);
            }

            lote.EsMerma = true;
            lote.FechaMerma = DateTime.Now;
            lote.Activo = false;
            lote.AlertaVencimientoSilenciadaHasta = null;

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();
            return NoContent();
        }

        // GET: api/lotes/reporte-mermas
        [HttpGet("reporte-mermas")]
        public async Task<ActionResult<IEnumerable<Lote>>> GetReporteMermas([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var query = _context.Lotes.AsNoTracking().Include(l => l.Producto).Where(l => l.EsMerma);

            if (desde.HasValue)
                query = query.Where(l => l.FechaMerma >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(l => l.FechaMerma <= hasta.Value.Date.AddDays(1).AddTicks(-1));

            return await query.OrderByDescending(l => l.FechaMerma).ToListAsync();
        }

        [HttpGet("reporte-mermas-periodo")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<List<PeriodoResumen>>> GetReporteMermasPeriodo(
            [FromQuery] DateTime desde, [FromQuery] DateTime hasta, [FromQuery] string agrupacion = "dia")
        {
            var inicio = desde.Date;
            var fin = hasta.Date.AddDays(1); // exclusivo
            var mermas = await _context.Lotes
                .AsNoTracking()
                .Where(l => l.EsMerma && l.FechaMerma.HasValue
                         && l.FechaMerma.Value >= inicio
                         && l.FechaMerma.Value < fin)
                .ToListAsync();

            var cultura = CultureInfo.GetCultureInfo("es-ES");

            (DateTime orden, string etiqueta) ClaveDe(Lote l)
            {
                var fecha = l.FechaMerma!.Value;
                return agrupacion.ToLower() switch
                {
                    "semana" => ObtenerClaveSemana(fecha),
                    "mes" => (new DateTime(fecha.Year, fecha.Month, 1),
                              Capitalizar(cultura.DateTimeFormat.GetMonthName(fecha.Month)) + $" {fecha.Year}"),
                    _ => (fecha.Date, fecha.ToString("dd/MM/yyyy"))
                };
            }

            var resumen = mermas
                .GroupBy(ClaveDe)
                .Select(g => new PeriodoResumen
                {
                    Etiqueta = g.Key.etiqueta,
                    FechaOrden = g.Key.orden,
                    TotalVentas = 0,
                    CostoTotal = g.Sum(l => l.CantidadActual * l.PrecioCosto),
                    Utilidad = -g.Sum(l => l.CantidadActual * l.PrecioCosto)
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
