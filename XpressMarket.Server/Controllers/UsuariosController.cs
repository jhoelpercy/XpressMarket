using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XpressMarket.Server.Data;
using XpressMarket.Shared.Models;

namespace XpressMarket.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador")]
    public class UsuariosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsuariosController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UsuarioResponse>>> GetUsuarios()
        {
            return await _context.Usuarios
                .Select(u => new UsuarioResponse
                {
                    Id = u.Id,
                    NombreCompleto = u.NombreCompleto ?? string.Empty,
                    NombreUsuario = u.NombreUsuario,
                    Rol = u.Rol,
                    Activo = u.Activo
                })
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UsuarioResponse>> GetUsuario(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
                return NotFound();

            return new UsuarioResponse
            {
                Id = usuario.Id,
                NombreCompleto = usuario.NombreCompleto ?? string.Empty,
                NombreUsuario = usuario.NombreUsuario,
                Rol = usuario.Rol,
                Activo = usuario.Activo
            };
        }

        [HttpPost]
        public async Task<ActionResult<UsuarioResponse>> PostUsuario(UsuarioCreateRequest request)
        {
            // Normalizar a minúsculas o trim para evitar duplicados como "Admin" y "admin "
            var nombreUsuarioNormalizado = request.NombreUsuario.Trim();

            if (await _context.Usuarios.AnyAsync(u => u.NombreUsuario.ToLower() == nombreUsuarioNormalizado.ToLower()))
                return BadRequest("Ese nombre de usuario ya está en uso.");

            var usuario = new Usuario
            {
                NombreCompleto = request.NombreCompleto?.Trim(),
                NombreUsuario = nombreUsuarioNormalizado,
                ContrasenaHash = BCrypt.Net.BCrypt.HashPassword(request.Contrasena),
                Rol = request.Rol,
                Activo = true
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            var response = new UsuarioResponse
            {
                Id = usuario.Id,
                NombreCompleto = usuario.NombreCompleto ?? string.Empty,
                NombreUsuario = usuario.NombreUsuario,
                Rol = usuario.Rol,
                Activo = usuario.Activo
            };

            return CreatedAtAction(nameof(GetUsuario), new { id = usuario.Id }, response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUsuario(int id, UsuarioUpdateRequest request)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();

            // Evita que el admin actual se desactive o se despoje del rol de Administrador
            var idActual = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idActual == id.ToString())
            {
                if (!request.Activo)
                    return BadRequest("No puedes desactivar tu propia cuenta.");

                if (request.Rol != "Administrador")
                    return BadRequest("No puedes cambiar tu propio rol de Administrador.");
            }

            usuario.Rol = request.Rol;
            usuario.Activo = request.Activo;

            if (!string.IsNullOrWhiteSpace(request.NuevaContrasena))
                usuario.ContrasenaHash = BCrypt.Net.BCrypt.HashPassword(request.NuevaContrasena);

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}