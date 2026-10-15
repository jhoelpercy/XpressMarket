using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;
using XpressMarket.Server.Data;
using XpressMarket.Shared.Models;

namespace XpressMarket.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Permite el acceso a usuarios autenticados (como el Cajero para consultas)
    public class ProductosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductosController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/productos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductos()
        {
            return await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Where(p => p.Activo)
                .ToListAsync();
        }

        // GET: api/productos/paginado?pagina=1&tamano=20&q=leche&categoriaId=3
        // El filtrado, el orden y el paginado ocurren en MySQL: sólo viaja la página solicitada.
        [HttpGet("paginado")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<PagedResult<Producto>>> GetPaginado(
            [FromQuery] int pagina = 1, [FromQuery] int tamano = 20,
            [FromQuery] string? q = null, [FromQuery] int? categoriaId = null)
        {
            tamano = Math.Clamp(tamano, 1, 100);
            pagina = Math.Max(1, pagina);

            var query = _context.Productos.AsNoTracking().Where(p => p.Activo);

            if (categoriaId.HasValue)
                query = query.Where(p => p.CategoriaId == categoriaId.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                query = query.Where(p => p.Nombre.Contains(t)
                    || (p.CodigoBarras != null && p.CodigoBarras.Contains(t))
                    || (p.Descripcion != null && p.Descripcion.Contains(t)));
            }

            var total = await query.CountAsync();
            var items = await query
                .Include(p => p.Categoria)
                .OrderBy(p => p.Nombre).ThenBy(p => p.Id)
                .Skip((pagina - 1) * tamano).Take(tamano)
                .ToListAsync();

            return new PagedResult<Producto> { Items = items, Total = total, Pagina = pagina, Tamano = tamano };
        }

        // GET: api/productos/buscar?q=lec&max=20
        // Buscador del punto de venta: devuelve sólo las mejores coincidencias, sin categoría.
        [HttpGet("buscar")]
        public async Task<ActionResult<IEnumerable<Producto>>> Buscar([FromQuery] string? q = null, [FromQuery] int max = 20)
        {
            max = Math.Clamp(max, 1, 50);
            var query = _context.Productos.AsNoTracking().Where(p => p.Activo);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                query = query.Where(p => p.Nombre.Contains(t)
                    || (p.CodigoBarras != null && p.CodigoBarras.Contains(t))
                    || (p.Descripcion != null && p.Descripcion.Contains(t)));
            }

            return await query.OrderBy(p => p.Nombre).Take(max).ToListAsync();
        }

        // GET: api/productos/resumen-inventario
        // Conteos para el inicio del administrador, calculados en la base de datos.
        [HttpGet("resumen-inventario")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<ResumenInventario>> GetResumenInventario()
        {
            var hoy = DateTime.Now.Date;
            var limite = hoy.AddDays(31);

            return new ResumenInventario
            {
                TotalProductos = await _context.Productos.CountAsync(p => p.Activo),
                StockBajo = await _context.Productos.CountAsync(p => p.Activo && p.StockActual <= p.StockMinimo),
                LotesPorVencer = await _context.Lotes.CountAsync(l => l.Activo && l.CantidadActual > 0
                    && l.FechaVencimiento >= hoy && l.FechaVencimiento < limite),
                LotesVencidos = await _context.Lotes.CountAsync(l => l.Activo && l.CantidadActual > 0
                    && l.FechaVencimiento < hoy),
            };
        }

        // GET: api/productos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Producto>> GetProducto(int id)
        {
            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Lotes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
                return NotFound();

            return producto;
        }

        // GET: api/productos/stock-bajo
        [HttpGet("stock-bajo")]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductosStockBajo()
        {
            // La condición se evalúa en MySQL (antes se traían todos los productos y se filtraba en memoria).
            return await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Where(p => p.Activo && p.StockActual <= p.StockMinimo)
                .ToListAsync();
        }

        // POST: api/productos
        [HttpPost]
        [Authorize(Roles = "Administrador")] // el cajero solo consulta el catálogo
        public async Task<ActionResult<Producto>> PostProducto(Producto producto)
        {
            if (!await _context.Categorias.AnyAsync(c => c.Id == producto.CategoriaId))
                return BadRequest("La categoría especificada no existe.");

            producto.FechaRegistro = DateTime.Now;
            producto.StockActual = 0; // El stock solo crece cuando se registran Lotes

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, producto);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> PutProducto(int id, Producto producto)
        {
            if (id != producto.Id)
                return BadRequest("El ID de la ruta no coincide con el del producto.");

            _context.Entry(producto).State = EntityState.Modified;
            _context.Entry(producto).Property(p => p.FechaRegistro).IsModified = false;
            _context.Entry(producto).Property(p => p.StockActual).IsModified = false;
            _context.Entry(producto).Property(p => p.PrecioCosto).IsModified = false; 

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Productos.AnyAsync(p => p.Id == id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/productos/5  (baja lógica, no física)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")] // Restringido solo para Administradores
        public async Task<IActionResult> DeleteProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound();
            if (await _context.Lotes.AnyAsync(l => l.ProductoId == id && l.Activo && l.CantidadActual > 0))
                return BadRequest("No se puede desactivar: el producto todavía tiene stock en lotes activos.");
            producto.Activo = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}