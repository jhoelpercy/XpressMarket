using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XpressMarket.Server.Data;
using XpressMarket.Server.Services;
using XpressMarket.Shared.Models;

namespace XpressMarket.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador")]
    public class ReportesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("informe-gerencial")]
        public async Task<ActionResult<InformeGerencial>> GetInformeGerencial(
            [FromQuery] DateTime desde, [FromQuery] DateTime hasta)
        {
            var finDia = hasta.Date.AddDays(1).AddTicks(-1);

            var ventas = await _context.Ventas
                .Include(v => v.Detalles).ThenInclude(d => d.Producto)
                .Where(v => v.Estado == EstadoVenta.Completada && v.FechaVenta >= desde.Date && v.FechaVenta <= finDia)
                .ToListAsync();

            var costoTotal = ventas.Sum(v => v.Detalles.Sum(d => d.Cantidad * (d.Producto?.PrecioCosto ?? 0)));
            var totalVentas = ventas.Sum(v => v.Total);

            var mermas = await _context.Lotes
                .Where(l => l.EsMerma && l.FechaMerma.HasValue && l.FechaMerma.Value >= desde.Date && l.FechaMerma.Value <= finDia)
                .ToListAsync();

            var stockBajo = await _context.Productos.Where(p => p.Activo).ToListAsync();

            var lotesVencidos = await _context.Lotes
                .Include(l => l.Producto)
                .Where(l => l.Activo && l.FechaVencimiento.Date < DateTime.Now.Date)
                .ToListAsync();

            var lotesPorVencer = await _context.Lotes
                .Include(l => l.Producto)
                .Where(l => l.Activo && l.FechaVencimiento.Date >= DateTime.Now.Date && l.FechaVencimiento.Date <= DateTime.Now.Date.AddDays(30))
                .ToListAsync();

            var topProductos = ventas
                .SelectMany(v => v.Detalles)
                .GroupBy(d => d.NombreProducto)
                .Select(g => new ProductoMasVendido { NombreProducto = g.Key, CantidadVendida = g.Sum(d => d.Cantidad) })
                .OrderByDescending(p => p.CantidadVendida)
                .Take(5)
                .ToList();

            return new InformeGerencial
            {
                Desde = desde,
                Hasta = hasta,
                CantidadVentas = ventas.Count,
                TotalVentas = totalVentas,
                TotalCosto = costoTotal,
                TotalUtilidad = totalVentas - costoTotal,
                CantidadMermas = mermas.Sum(l => l.CantidadActual),
                ValorMermas = mermas.Sum(l => l.CantidadActual * l.PrecioCosto),
                ProductosStockBajo = stockBajo.Where(p => p.StockBajo)
                    .Select(p => new ProductoResumen { Nombre = p.Nombre, StockActual = p.StockActual, StockMinimo = p.StockMinimo })
                    .ToList(),
                LotesVencidos = lotesVencidos
                    .Select(l => new LoteResumen { NumeroLote = l.NumeroLote, Producto = l.Producto?.Nombre ?? "", FechaVencimiento = l.FechaVencimiento, Cantidad = l.CantidadActual })
                    .ToList(),
                LotesProximosAVencer = lotesPorVencer
                    .Select(l => new LoteResumen { NumeroLote = l.NumeroLote, Producto = l.Producto?.Nombre ?? "", FechaVencimiento = l.FechaVencimiento, Cantidad = l.CantidadActual })
                    .ToList(),
                TopProductos = topProductos
            };
        }

        [HttpGet("informe-gerencial/pdf")]
        public async Task<IActionResult> GetInformeGerencialPdf([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
        {
            var informeResult = await GetInformeGerencial(desde, hasta);
            if (informeResult.Result is not null || informeResult.Value is null)
                return BadRequest("No se pudo generar el informe.");

            var pdfBytes = InformeGerencialPdfService.Generar(informeResult.Value);
            return File(pdfBytes, "application/pdf", $"Informe_Gerencial_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.pdf");
        }
    }
}