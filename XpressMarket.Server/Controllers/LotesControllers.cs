using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using XpressMarket.Server.Data;
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
                "vencidos" => query.Where(l => l.FechaVencimiento < hoy),
                "por-vencer" => query.Where(l => l.FechaVencimiento >= hoy && l.FechaVencimiento < hoy.AddDays(31)),
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
        [HttpGet("proximos-a-vencer")]
        public async Task<ActionResult<IEnumerable<Lote>>> GetProximosAVencer()
        {
            // Se compara contra límites de fecha ya calculados (sin .Date sobre la columna)
            // para que MySQL pueda usar el índice de FechaVencimiento.
            var hoy = DateTime.Now.Date;
            var limite = hoy.AddDays(31); // exclusivo: incluye todo el día 30

            return await _context.Lotes
                .AsNoTracking()
                .Include(l => l.Producto)
                .Where(l => l.Activo
                    && l.CantidadActual > 0 // CORRECCIÓN: Filtra lotes agotados
                    && l.FechaVencimiento >= hoy
                    && l.FechaVencimiento < limite)
                .OrderBy(l => l.FechaVencimiento)
                .ToListAsync();
        }

        // GET: api/lotes/vencidos
        [HttpGet("vencidos")]
        public async Task<ActionResult<IEnumerable<Lote>>> GetVencidos()
        {
            var hoy = DateTime.Now.Date;

            return await _context.Lotes
                .AsNoTracking()
                .Include(l => l.Producto)
                .Where(l => l.Activo
                    && l.CantidadActual > 0 // CORRECCIÓN: Filtra lotes agotados
                    && l.FechaVencimiento < hoy)
                .OrderBy(l => l.FechaVencimiento)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Lote>> PostLote(LoteCreateRequest request)
        {
            var lote = request.Lote;

            var producto = await _context.Productos.FindAsync(lote.ProductoId);
            if (producto == null)
                return BadRequest("El producto especificado no existe.");

            lote.FechaIngreso = DateTime.Now;
            if (lote.CantidadActual == 0)
                lote.CantidadActual = lote.CantidadInicial;

            lote.RegistradoPorUsuarioId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
            lote.RegistradoPorNombre = User.FindFirst(ClaimTypes.Name)?.Value;

            // El stock físico del producto crece con cada lote que entra al inventario.
            producto.StockActual += lote.CantidadActual;

            if (request.ActualizarCostoProducto || producto.PrecioCosto == 0)
                producto.PrecioCosto = lote.PrecioCosto;

            if (request.NuevoPrecioVenta.HasValue && request.NuevoPrecioVenta.Value > 0)
                producto.PrecioVenta = request.NuevoPrecioVenta.Value;

            _context.Lotes.Add(lote);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLote), new { id = lote.Id }, lote);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutLote(int id, LoteUpdateRequest request)
        {
            var loteNuevo = request.Lote;
            if (id != loteNuevo.Id)
                return BadRequest("El ID de la ruta no coincide con el del lote.");

            var loteExistente = await _context.Lotes.FindAsync(id);
            if (loteExistente == null)
                return NotFound();

            var producto = await _context.Productos.FindAsync(loteExistente.ProductoId);
            if (producto == null)
                return BadRequest("El producto asociado no existe.");

            // Ajusta el stock del producto por la diferencia entre la cantidad vieja y la nueva
            var deltaCantidad = loteNuevo.CantidadActual - loteExistente.CantidadActual;
            producto.StockActual += deltaCantidad;

            if (request.ActualizarCostoProducto)
                producto.PrecioCosto = loteNuevo.PrecioCosto;

            if (request.NuevoPrecioVenta.HasValue && request.NuevoPrecioVenta.Value > 0)
                producto.PrecioVenta = request.NuevoPrecioVenta.Value;

            loteExistente.NumeroLote = loteNuevo.NumeroLote;
            loteExistente.ProveedorId = loteNuevo.ProveedorId;
            loteExistente.FechaVencimiento = loteNuevo.FechaVencimiento;
            loteExistente.CantidadInicial = loteNuevo.CantidadInicial;
            loteExistente.CantidadActual = loteNuevo.CantidadActual;
            loteExistente.PrecioCosto = loteNuevo.PrecioCosto;
            loteExistente.Activo = loteNuevo.Activo;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Lotes.AnyAsync(l => l.Id == id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLote(int id)
        {
            var lote = await _context.Lotes.FindAsync(id);
            if (lote == null)
                return NotFound();

            if (lote.Activo && lote.CantidadActual > 0)
            {
                var producto = await _context.Productos.FindAsync(lote.ProductoId);
                if (producto != null)
                    producto.StockActual -= lote.CantidadActual;
            }

            lote.Activo = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/lotes/5/registrar-merma
        [HttpPut("{id}/registrar-merma")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> RegistrarMerma(int id)
        {
            var lote = await _context.Lotes.FindAsync(id);
            if (lote == null)
                return NotFound();

            if (!lote.Vencido)
                return BadRequest("Solo se pueden registrar como merma los lotes vencidos.");

            if (lote.EsMerma)
                return BadRequest("Este lote ya fue registrado como merma.");

            if (lote.CantidadActual > 0)
            {
                var producto = await _context.Productos.FindAsync(lote.ProductoId);
                if (producto != null)
                    producto.StockActual -= lote.CantidadActual;
            }

            lote.EsMerma = true;
            lote.FechaMerma = DateTime.Now;
            lote.Activo = false;

            await _context.SaveChangesAsync();
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
