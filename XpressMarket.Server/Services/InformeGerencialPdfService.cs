using QuestPDF.Fluent;
using QuestPDF.Helpers;
using XpressMarket.Shared.Models;

namespace XpressMarket.Server.Services
{
    public static class InformeGerencialPdfService
    {
        public static byte[] Generar(InformeGerencial informe)
        {
            var documento = Document.Create(container =>
            {
                container.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4);
                    pagina.Margin(30);
                    pagina.DefaultTextStyle(x => x.FontSize(10));

                    pagina.Header().Column(col =>
                    {
                        col.Item().Text("Xpress Market").FontSize(20).Bold().FontColor("#83328F");
                        col.Item().Text("Informe Gerencial").FontSize(13).FontColor(Colors.Grey.Darken1);
                        col.Item().Text($"Período: {informe.Desde:dd/MM/yyyy} al {informe.Hasta:dd/MM/yyyy}").FontSize(10);
                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor("#EC8423");
                    });

                    pagina.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Text("Resumen Financiero").Bold().FontSize(13).FontColor("#62266B");
                        col.Item().PaddingTop(3).Row(row =>
                        {
                            row.RelativeItem().Text($"Ventas: {informe.CantidadVentas}");
                            row.RelativeItem().Text($"Total Ventas: Bs {informe.TotalVentas:N2}");
                        });
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Costo Total: Bs {informe.TotalCosto:N2}");
                            row.RelativeItem().Text($"Utilidad: Bs {informe.TotalUtilidad:N2}").FontColor(informe.TotalUtilidad >= 0 ? Colors.Green.Darken1 : Colors.Red.Darken1).Bold();
                        });

                        col.Item().PaddingTop(15).Text("Mermas del Período").Bold().FontSize(13).FontColor("#62266B");
                        col.Item().Text($"Unidades perdidas: {informe.CantidadMermas}    Valor perdido: Bs {informe.ValorMermas:N2}");

                        col.Item().PaddingTop(15).Text("Productos con Stock Bajo").Bold().FontSize(13).FontColor("#62266B");
                        if (!informe.ProductosStockBajo.Any())
                        {
                            col.Item().Text("Ninguno.").Italic();
                        }
                        else
                        {
                            foreach (var p in informe.ProductosStockBajo)
                                col.Item().Text($"• {p.Nombre} — Stock: {p.StockActual} / Mínimo: {p.StockMinimo}");
                        }

                        col.Item().PaddingTop(15).Text("Lotes Vencidos (pendientes de dar de baja)").Bold().FontSize(13).FontColor("#62266B");
                        if (!informe.LotesVencidos.Any())
                        {
                            col.Item().Text("Ninguno.").Italic();
                        }
                        else
                        {
                            foreach (var l in informe.LotesVencidos)
                                col.Item().Text($"• {l.Producto} (Lote {l.NumeroLote}) — venció {l.FechaVencimiento:dd/MM/yyyy}, {l.Cantidad} unid.");
                        }

                        col.Item().PaddingTop(15).Text("Lotes Próximos a Vencer (30 días)").Bold().FontSize(13).FontColor("#62266B");
                        if (!informe.LotesProximosAVencer.Any())
                        {
                            col.Item().Text("Ninguno.").Italic();
                        }
                        else
                        {
                            foreach (var l in informe.LotesProximosAVencer)
                                col.Item().Text($"• {l.Producto} (Lote {l.NumeroLote}) — vence {l.FechaVencimiento:dd/MM/yyyy}, {l.Cantidad} unid.");
                        }

                        col.Item().PaddingTop(15).Text("Productos Más Vendidos en el Período").Bold().FontSize(13).FontColor("#62266B");
                        if (!informe.TopProductos.Any())
                        {
                            col.Item().Text("Sin ventas registradas en este período.").Italic();
                        }
                        else
                        {
                            foreach (var p in informe.TopProductos)
                                col.Item().Text($"• {p.NombreProducto} — {p.CantidadVendida} unidades vendidas");
                        }
                    });

                    pagina.Footer().AlignCenter().Text($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });

            return documento.GeneratePdf();
        }
    }
}