using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XpressMarket.Server.Data;
using XpressMarket.Server.Services;
using XpressMarket.Shared.Models;
using Microsoft.AspNetCore.Authorization;

namespace XpressMarket.Server.Controllers
{
    [Authorize(Roles = "Administrador")]
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriasController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Categoria>>> GetCategorias()
        {
            return await _context.Categorias
            .Where(c => c.Activo)
            .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Categoria>> GetCategoria(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null)
                return NotFound();

            return categoria;
        }

        [HttpPost]
        public async Task<ActionResult<Categoria>> PostCategoria(Categoria categoria)
        {
            categoria.Id = 0;
            categoria.Activo = true;
            categoria.Nombre = (categoria.Nombre ?? string.Empty).Trim();

            var error = await MensajeNombreDuplicadoAsync(categoria.Nombre, null);
            if (error != null)
                return BadRequest(error);

            try
            {
                _context.Categorias.Add(categoria);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ErroresBaseDeDatos.EsDuplicado(ex))
            {
                return BadRequest($"Ya existe una categoría llamada '{categoria.Nombre}'.");
            }

            return CreatedAtAction(nameof(GetCategoria), new { id = categoria.Id }, categoria);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutCategoria(int id, Categoria categoria)
        {
            if (id != categoria.Id)
                return BadRequest("El ID de la ruta no coincide con el de la categoría.");

            categoria.Nombre = (categoria.Nombre ?? string.Empty).Trim();

            var error = await MensajeNombreDuplicadoAsync(categoria.Nombre, id);
            if (error != null)
                return BadRequest(error);

            _context.Entry(categoria).State = EntityState.Modified;
            // La baja se hace sólo con DELETE, que verifica que no queden productos activos en la categoría.
            _context.Entry(categoria).Property(c => c.Activo).IsModified = false;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Categorias.AnyAsync(c => c.Id == id))
                    return NotFound();
                throw;
            }
            catch (DbUpdateException ex) when (ErroresBaseDeDatos.EsDuplicado(ex))
            {
                return BadRequest($"Ya existe una categoría llamada '{categoria.Nombre}'.");
            }

            return NoContent();
        }

        /// <summary>El nombre de la categoría es único (sin distinguir mayúsculas); devuelve el mensaje si ya existe.</summary>
        private async Task<string?> MensajeNombreDuplicadoAsync(string nombre, int? idActual)
        {
            if (nombre.Length < 2)
                return "El nombre debe tener entre 2 y 100 caracteres.";

            var otra = await _context.Categorias.AsNoTracking()
                .Where(c => c.Nombre == nombre && (idActual == null || c.Id != idActual))
                .Select(c => new { c.Activo })
                .FirstOrDefaultAsync();

            if (otra == null)
                return null;

            return otra.Activo
                ? $"Ya existe una categoría llamada '{nombre}'."
                : $"Ya existe una categoría desactivada llamada '{nombre}'. Usa otro nombre.";
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategoria(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null)
                return NotFound();
            if (await _context.Productos.AnyAsync(p => p.CategoriaId == id && p.Activo))
                return BadRequest("No se puede desactivar: hay productos activos en esta categoría.");

            categoria.Activo = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
