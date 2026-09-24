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
                .Include(p => p.Categoria)
                .Where(p => p.Activo)
                .ToListAsync();
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
            var productos = await _context.Productos
                .Include(p => p.Categoria)
                .Where(p => p.Activo)
                .ToListAsync();

            return productos.Where(p => p.StockBajo).ToList();
        }

        // POST: api/productos
        [HttpPost]
        [Authorize(Roles = "Administrador")] // Restringido solo para Administradores
        public async Task<ActionResult<Producto>> PostProducto(Producto producto)
        {
            if (!await _context.Categorias.AnyAsync(c => c.Id == producto.CategoriaId))
                return BadRequest("La categoría especificada no existe.");

            producto.FechaRegistro = DateTime.Now;
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, producto);
        }

        // PUT: api/productos/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")] // Restringido solo para Administradores
        public async Task<IActionResult> PutProducto(int id, Producto producto)
        {
            if (id != producto.Id)
                return BadRequest("El ID de la ruta no coincide con el del producto.");

            _context.Entry(producto).State = EntityState.Modified;
            _context.Entry(producto).Property(p => p.FechaRegistro).IsModified = false;

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

            producto.Activo = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}