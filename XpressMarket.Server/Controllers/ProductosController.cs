using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;
using XpressMarket.Server.Data;
using XpressMarket.Server.Services;
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
            var lista = await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Where(p => p.Activo)
                .ToListAsync();
            return OcultarCostos(lista);
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

            return OcultarCostos(await query.OrderBy(p => p.Nombre).Take(max).ToListAsync());
        }

        // GET: api/productos/resumen-inventario
        // Conteos para el inicio del administrador, calculados en la base de datos.
        [HttpGet("resumen-inventario")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<ResumenInventario>> GetResumenInventario()
        {
            var ahora = DateTime.Now;
            var hoy = ahora.Date;

            // Los indicadores cuentan alertas vigentes (las silenciadas desde el panel de alertas no se incluyen).
            return new ResumenInventario
            {
                TotalProductos = await _context.Productos.CountAsync(p => p.Activo),
                StockBajo = await _context.Productos.ConAlertaStockVigente(ahora).CountAsync(),
                LotesPorVencer = await _context.Lotes.PorVencerVigentes(hoy, ahora).CountAsync(),
                LotesVencidos = await _context.Lotes.Vencidos(hoy).CountAsync(),
            };
        }

        // GET: api/productos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Producto>> GetProducto(int id)
        {
            var producto = await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Include(p => p.Lotes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
                return NotFound();

            if (!User.IsInRole("Administrador"))
                producto.PrecioCosto = 0;

            return producto;
        }

        // GET: api/productos/stock-bajo
        [HttpGet("stock-bajo")]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductosStockBajo()
        {
            // Alertas vigentes: la condición se evalúa en MySQL y no incluye las silenciadas.
            var lista = await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .ConAlertaStockVigente(DateTime.Now)
                .ToListAsync();
            return OcultarCostos(lista);
        }

        // POST: api/productos
        [HttpPost]
        [Authorize(Roles = "Administrador")] // el cajero solo consulta el catálogo
        public async Task<ActionResult<Producto>> PostProducto(Producto producto)
        {
            producto.Id = 0;
            producto.Categoria = null;
            producto.Activo = true;
            producto.AlertaStockSilenciadaHasta = null;
            producto.Nombre = (producto.Nombre ?? string.Empty).Trim();

            var error = await ValidarProductoAsync(producto, null);
            if (error != null)
                return BadRequest(error);

            producto.FechaRegistro = DateTime.Now;
            producto.StockActual = 0; // El stock solo crece cuando se registran Lotes

            try
            {
                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ErroresBaseDeDatos.EsDuplicado(ex))
            {
                // Dos administradores guardaron el mismo nombre/código a la vez: gana el primero.
                return BadRequest("Ya existe un producto con ese nombre o ese código de barras.");
            }

            return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, producto);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> PutProducto(int id, Producto producto)
        {
            if (id != producto.Id)
                return BadRequest("El ID de la ruta no coincide con el del producto.");

            producto.Categoria = null;
            producto.Nombre = (producto.Nombre ?? string.Empty).Trim();

            var error = await ValidarProductoAsync(producto, id);
            if (error != null)
                return BadRequest(error);

            _context.Entry(producto).State = EntityState.Modified;
            // Estos campos no se editan desde el formulario del producto:
            // el stock y el costo cambian con los lotes, el estado con la baja (que revisa el stock)
            // y el silencio de la alerta con el panel de alertas.
            _context.Entry(producto).Property(p => p.FechaRegistro).IsModified = false;
            _context.Entry(producto).Property(p => p.StockActual).IsModified = false;
            _context.Entry(producto).Property(p => p.PrecioCosto).IsModified = false;
            _context.Entry(producto).Property(p => p.Activo).IsModified = false;
            _context.Entry(producto).Property(p => p.AlertaStockSilenciadaHasta).IsModified = false;

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
            catch (DbUpdateException ex) when (ErroresBaseDeDatos.EsDuplicado(ex))
            {
                return BadRequest("Ya existe un producto con ese nombre o ese código de barras.");
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

        /// <summary>El cajero consulta el catálogo pero no debe ver el costo de compra (ni, por tanto, el margen).</summary>
        private List<Producto> OcultarCostos(List<Producto> productos)
        {
            if (!User.IsInRole("Administrador"))
                foreach (var p in productos)
                    p.PrecioCosto = 0;
            return productos;
        }

        /// <summary>Reglas comunes al crear y al modificar: categoría válida, nombre único y código de barras único.</summary>
        private async Task<string?> ValidarProductoAsync(Producto producto, int? idActual)
        {
            if (producto.Nombre.Length < 2)
                return "El nombre debe tener entre 2 y 150 caracteres.";

            if (!await _context.Categorias.AnyAsync(c => c.Id == producto.CategoriaId && c.Activo))
                return "La categoría especificada no existe o está desactivada.";

            // El nombre no puede repetirse (la comparación no distingue mayúsculas ni tildes, igual que el índice único).
            var mismoNombre = await _context.Productos.AsNoTracking()
                .Where(p => p.Nombre == producto.Nombre && (idActual == null || p.Id != idActual))
                .Select(p => new { p.Activo })
                .FirstOrDefaultAsync();
            if (mismoNombre != null)
                return mismoNombre.Activo
                    ? $"Ya existe un producto llamado '{producto.Nombre}'. Cada producto debe tener un nombre único."
                    : $"Ya existe un producto desactivado llamado '{producto.Nombre}'. Usa otro nombre.";

            // El código de barras también es único (vacío = producto sin código).
            producto.CodigoBarras = NormalizarCodigo(producto.CodigoBarras);
            return await MensajeCodigoDuplicadoAsync(producto.CodigoBarras, idActual);
        }

        /// <summary>Recorta espacios; un código vacío se guarda como NULL (producto sin código).</summary>
        private static string? NormalizarCodigo(string? codigo)
        {
            var c = codigo?.Trim();
            return string.IsNullOrEmpty(c) ? null : c;
        }

        /// <summary>Devuelve el mensaje de error si otro producto ya usa ese código de barras; si no, null.</summary>
        private async Task<string?> MensajeCodigoDuplicadoAsync(string? codigo, int? idActual)
        {
            if (codigo == null)
                return null;

            var otro = await _context.Productos.AsNoTracking()
                .Where(p => p.CodigoBarras == codigo && (idActual == null || p.Id != idActual))
                .Select(p => new { p.Nombre, p.Activo })
                .FirstOrDefaultAsync();

            if (otro == null)
                return null;

            return otro.Activo
                ? $"El código de barras '{codigo}' ya está registrado en el producto '{otro.Nombre}'."
                : $"El código de barras '{codigo}' pertenece al producto desactivado '{otro.Nombre}'.";
        }
    }
}