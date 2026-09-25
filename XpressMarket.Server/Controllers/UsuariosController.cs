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
                    NombreUsuario = u.NombreUsuario,
                    Rol = u.Rol,
                    Activo = u.Activo
                })
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<UsuarioResponse>> PostUsuario(UsuarioCreateRequest request)
        {
            if (await _context.Usuarios.AnyAsync(u => u.NombreUsuario == request.NombreUsuario))
                return BadRequest("Ese nombre de usuario ya está en uso.");

            var usuario = new Usuario
            {
                NombreUsuario = request.NombreUsuario,
                ContrasenaHash = BCrypt.Net.BCrypt.HashPassword(request.Contrasena),
                Rol = request.Rol,
                Activo = true
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return Ok(new UsuarioResponse
            {
                Id = usuario.Id,
                NombreUsuario = usuario.NombreUsuario,
                Rol = usuario.Rol,
                Activo = usuario.Activo
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUsuario(int id, UsuarioUpdateRequest request)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();

            // Evita que el admin se desactive a sí mismo y se quede sin acceso
            var idActual = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idActual == id.ToString() && !request.Activo)
                return BadRequest("No puedes desactivar tu propia cuenta.");

            usuario.Rol = request.Rol;
            usuario.Activo = request.Activo;

            if (!string.IsNullOrWhiteSpace(request.NuevaContrasena))
                usuario.ContrasenaHash = BCrypt.Net.BCrypt.HashPassword(request.NuevaContrasena);

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}