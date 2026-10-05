using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;
using XpressMarket.Server.Data;
using XpressMarket.Shared.Models;

namespace XpressMarket.Server.Controllers
{
    [Authorize(Roles = "Administrador")]
    [ApiController]
    [Route("api/reportes")]
    public class ReportesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportesController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // REPORTE DE MERMAS
        // ============================================================

        private async Task<List<Lote>> ObtenerMermasAsync(DateTime? desde, DateTime? hasta)
        {
            var query = _context.Lotes
                .Include(l => l.Producto)
                .Where(l => l.EsMerma);

            if (desde.HasValue)
                query = query.Where(l => l.FechaMerma >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(l => l.FechaMerma <= hasta.Value.Date.AddDays(1).AddTicks(-1));

            return await query.OrderByDescending(l => l.FechaMerma).ToListAsync();
        }

        [HttpGet("mermas/excel")]
        public async Task<IActionResult> MermasExcel([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var mermas = await ObtenerMermasAsync(desde, hasta);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Mermas");

            // Encabezados
            string[] headers = { "N° Lote", "Producto", "Fecha Vencimiento", "Fecha Merma", "Cantidad", "Costo Unit. (Bs)", "Valor Perdido (Bs)" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#6b2181");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Datos
            int fila = 2;
            foreach (var m in mermas)
            {
                ws.Cell(fila, 1).Value = m.NumeroLote;
                ws.Cell(fila, 2).Value = m.Producto?.Nombre ?? "-";
                ws.Cell(fila, 3).Value = m.FechaVencimiento.ToString("dd/MM/yyyy");
                ws.Cell(fila, 4).Value = m.FechaMerma?.ToString("dd/MM/yyyy HH:mm") ?? "-";
                ws.Cell(fila, 5).Value = m.CantidadActual;
                ws.Cell(fila, 6).Value = m.PrecioCosto;
                ws.Cell(fila, 7).Value = m.CantidadActual * m.PrecioCosto;
                fila++;
            }

            // Total al final
            ws.Cell(fila, 6).Value = "TOTAL:";
            ws.Cell(fila, 6).Style.Font.Bold = true;
            ws.Cell(fila, 7).FormulaA1 = $"SUM(G2:G{fila - 1})";
            ws.Cell(fila, 7).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Reporte_Mermas_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        [HttpGet("mermas/pdf")]
        public async Task<IActionResult> MermasPdf([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var mermas = await ObtenerMermasAsync(desde, hasta);
            var totalPerdido = mermas.Sum(m => m.CantidadActual * m.PrecioCosto);

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

                    // HEADER
                    page.Header().Column(col =>
                    {
                        col.Item().Text("XPRESSMARKET").Bold().FontSize(16).FontColor("#6b2181");
                        col.Item().Text($"Reporte de Mermas - Generado: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9);
                        col.Item().Text($"Período: {(desde?.ToString("dd/MM/yyyy") ?? "Inicio")} - {(hasta?.ToString("dd/MM/yyyy") ?? "Hoy")}").FontSize(9);
                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor("#6b2181");
                    });

                    // CONTENT
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().PaddingBottom(8).Row(row =>
                        {
                            row.RelativeItem().Text($"Total de mermas: {mermas.Count}").Bold().FontSize(10);
                            row.RelativeItem().AlignRight().Text($"Valor total perdido: Bs. {totalPerdido:N2}")
                                .Bold().FontSize(11).FontColor("#d32f2f");
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.3f);
                                columns.RelativeColumn(1.3f);
                            });

                            table.Header(header =>
                            {
                                void HeaderCell(string text) =>
                                    header.Cell().Background("#6b2181").Padding(4)
                                        .Text(text).FontColor("#ffffff").Bold().FontSize(8);

                                HeaderCell("N° Lote");
                                HeaderCell("Producto");
                                HeaderCell("Vencimiento");
                                HeaderCell("Fecha Merma");
                                HeaderCell("Cant.");
                                HeaderCell("Costo U.");
                                HeaderCell("Valor Perdido");
                            });

                            foreach (var m in mermas)
                            {
                                void BodyCell(string text) =>
                                    table.Cell().BorderBottom(0.5f).BorderColor("#e0e0e0")
                                        .Padding(4).Text(text).FontSize(8);

                                BodyCell(m.NumeroLote);
                                BodyCell(m.Producto?.Nombre ?? "-");
                                BodyCell(m.FechaVencimiento.ToString("dd/MM/yyyy"));
                                BodyCell(m.FechaMerma?.ToString("dd/MM/yyyy") ?? "-");
                                BodyCell(m.CantidadActual.ToString());
                                BodyCell($"Bs. {m.PrecioCosto:N2}");
                                BodyCell($"Bs. {m.CantidadActual * m.PrecioCosto:N2}");
                            }
                        });
                    });

                    // FOOTER
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Página ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                        x.Span(" de ").FontSize(8);
                        x.TotalPages().FontSize(8);
                    });
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", $"Reporte_Mermas_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
        }

        // ============================================================
        // REPORTE DE UTILIDADES
        // ============================================================

        private async Task<List<Venta>> ObtenerVentasAsync(DateTime? desde, DateTime? hasta)
        {
            var query = _context.Ventas
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(v => v.Estado == EstadoVenta.Completada);

            if (desde.HasValue)
                query = query.Where(v => v.FechaVenta >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(v => v.FechaVenta <= hasta.Value.Date.AddDays(1).AddTicks(-1));

            return await query.OrderByDescending(v => v.FechaVenta).ToListAsync();
        }

        [HttpGet("utilidades/excel")]
        public async Task<IActionResult> UtilidadesExcel([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var ventas = await ObtenerVentasAsync(desde, hasta);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Utilidades");

            string[] headers = { "N° Venta", "Fecha", "Cajero", "Total Venta (Bs)", "Costo Total (Bs)", "Utilidad (Bs)", "Margen %" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#6b2181");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int fila = 2;
            foreach (var v in ventas)
            {
                var costo = v.Detalles.Sum(d => d.Cantidad * (d.Producto?.PrecioCosto ?? 0));
                var utilidad = v.Total - costo;
                var margen = v.Total > 0 ? (utilidad / v.Total) * 100 : 0;

                ws.Cell(fila, 1).Value = v.Id;
                ws.Cell(fila, 2).Value = v.FechaVenta.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(fila, 3).Value = v.NombreUsuario;
                ws.Cell(fila, 4).Value = v.Total;
                ws.Cell(fila, 5).Value = costo;
                ws.Cell(fila, 6).Value = utilidad;
                ws.Cell(fila, 7).Value = margen;
                fila++;
            }

            // Totales
            ws.Cell(fila, 3).Value = "TOTALES:";
            ws.Cell(fila, 3).Style.Font.Bold = true;
            ws.Cell(fila, 4).FormulaA1 = $"SUM(D2:D{fila - 1})";
            ws.Cell(fila, 5).FormulaA1 = $"SUM(E2:E{fila - 1})";
            ws.Cell(fila, 6).FormulaA1 = $"SUM(F2:F{fila - 1})";
            ws.Range(fila, 4, fila, 6).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Reporte_Utilidades_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        [HttpGet("utilidades/pdf")]
        public async Task<IActionResult> UtilidadesPdf([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var ventas = await ObtenerVentasAsync(desde, hasta);

            var totalVentas = ventas.Sum(v => v.Total);
            var totalCosto = ventas.Sum(v => v.Detalles.Sum(d => d.Cantidad * (d.Producto?.PrecioCosto ?? 0)));
            var totalUtilidad = totalVentas - totalCosto;

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("XPRESSMARKET").Bold().FontSize(16).FontColor("#6b2181");
                        col.Item().Text($"Reporte de Utilidades - Generado: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9);
                        col.Item().Text($"Período: {(desde?.ToString("dd/MM/yyyy") ?? "Inicio")} - {(hasta?.ToString("dd/MM/yyyy") ?? "Hoy")}").FontSize(9);
                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor("#6b2181");
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().PaddingBottom(8).Row(row =>
                        {
                            row.RelativeItem().Text($"Ventas: Bs. {totalVentas:N2}").Bold();
                            row.RelativeItem().Text($"Costo: Bs. {totalCosto:N2}").Bold();
                            row.RelativeItem().Text($"Utilidad: Bs. {totalUtilidad:N2}")
                                .Bold().FontColor(totalUtilidad >= 0 ? "#2e7d32" : "#d32f2f");
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(header =>
                            {
                                void HeaderCell(string text) =>
                                    header.Cell().Background("#6b2181").Padding(4)
                                        .Text(text).FontColor("#ffffff").Bold().FontSize(8);

                                HeaderCell("N°");
                                HeaderCell("Fecha");
                                HeaderCell("Cajero");
                                HeaderCell("Venta");
                                HeaderCell("Costo");
                                HeaderCell("Utilidad");
                            });

                            foreach (var v in ventas)
                            {
                                var costo = v.Detalles.Sum(d => d.Cantidad * (d.Producto?.PrecioCosto ?? 0));
                                var utilidad = v.Total - costo;

                                table.Cell().BorderBottom(0.5f).BorderColor("#e0e0e0").Padding(4).Text(v.Id.ToString()).FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e0e0e0").Padding(4).Text(v.FechaVenta.ToString("dd/MM/yyyy")).FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e0e0e0").Padding(4).Text(v.NombreUsuario).FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e0e0e0").Padding(4).Text($"Bs. {v.Total:N2}").FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e0e0e0").Padding(4).Text($"Bs. {costo:N2}").FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e0e0e0").Padding(4)
                                    .Text($"Bs. {utilidad:N2}")
                                    .FontColor(utilidad >= 0 ? "#2e7d32" : "#d32f2f").FontSize(8);
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Página ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                        x.Span(" de ").FontSize(8);
                        x.TotalPages().FontSize(8);
                    });
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", $"Reporte_Utilidades_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
        }
    }
}