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
                .Include(l => l.Producto)
                .Include(l => l.Proveedor)
                .Where(l => l.Activo)
                .ToListAsync();
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
            var limite = DateTime.Now.Date.AddDays(30);

            return await _context.Lotes
                .Include(l => l.Producto)
                .Where(l => l.Activo
                    && l.FechaVencimiento.Date >= DateTime.Now.Date
                    && l.FechaVencimiento.Date <= limite)
                .OrderBy(l => l.FechaVencimiento)
                .ToListAsync();
        }

        // GET: api/lotes/vencidos
        [HttpGet("vencidos")]
        public async Task<ActionResult<IEnumerable<Lote>>> GetVencidos()
        {
            return await _context.Lotes
                .Include(l => l.Producto)
                .Where(l => l.Activo && l.FechaVencimiento.Date < DateTime.Now.Date)
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

            if (request.ActualizarCostoProducto)
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

            // Ajusta el stock del producto por la diferencia entre la cantidad vieja y la nueva,
            // no por el valor absoluto (evita duplicar o perder unidades ya contadas).
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

        // GET: api/lotes/reporte-mermas?desde=2026-01-01&hasta=2026-12-31
        [HttpGet("reporte-mermas")]
        public async Task<ActionResult<IEnumerable<Lote>>> GetReporteMermas([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var query = _context.Lotes.Include(l => l.Producto).Where(l => l.EsMerma);

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
            var mermas = await _context.Lotes
                .Where(l => l.EsMerma && l.FechaMerma.HasValue
                         && l.FechaMerma.Value.Date >= desde.Date
                         && l.FechaMerma.Value.Date <= hasta.Date)
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
                    Utilidad = -g.Sum(l => l.CantidadActual * l.PrecioCosto) // Pérdida = utilidad negativa
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
