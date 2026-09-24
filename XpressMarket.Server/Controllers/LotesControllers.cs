using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XpressMarket.Server.Data;
using XpressMarket.Shared.Models;
using Microsoft.AspNetCore.Authorization;
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
        public async Task<ActionResult<Lote>> PostLote(Lote lote)
        {
            if (!await _context.Productos.AnyAsync(p => p.Id == lote.ProductoId))
                return BadRequest("El producto especificado no existe.");

            lote.FechaIngreso = DateTime.Now;
            if (lote.CantidadActual == 0)
                lote.CantidadActual = lote.CantidadInicial;

            _context.Lotes.Add(lote);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLote), new { id = lote.Id }, lote);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutLote(int id, Lote lote)
        {
            if (id != lote.Id)
                return BadRequest("El ID de la ruta no coincide con el del lote.");

            _context.Entry(lote).State = EntityState.Modified;
            _context.Entry(lote).Property(l => l.FechaIngreso).IsModified = false;

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
    }
}    
