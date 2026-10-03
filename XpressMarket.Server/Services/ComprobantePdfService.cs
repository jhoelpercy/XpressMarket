// En ComprobantePdfService.cs
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using XpressMarket.Shared.Models;

namespace XpressMarket.Server.Services
{
    public static class ComprobantePdfService
    {
        public static byte[] Generar(Venta venta)
        {
            // Configurar licencias y asegurar uso de fuentes del sistema estandarizadas
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseSystemFonts = true;

            var pdfDocument = Document.Create(container =>
            {
                container.Page(pagina =>
                {
                    pagina.Size(PageSizes.A5);
                    pagina.Margin(25);
                    // Uso de Arial estándar para evitar fallas nativas al resolver 'Lato' en SkiaSharp
                    pagina.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(10));

                    pagina.Header().Column(col =>
                    {
                        col.Item().Text("Xpress Market").FontSize(18).Bold().FontColor("#1F3D2B");
                        col.Item().Text("Comprobante de Venta").FontSize(11).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor("#D9A544");
                    });

                    pagina.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"N° Venta: {venta.Id}");
                            row.RelativeItem().AlignRight().Text($"Fecha: {venta.FechaVenta:dd/MM/yyyy HH:mm}");
                        });
                        col.Item().Text($"Atendido por: {venta.NombreUsuario ?? "Cajero"}");
                        col.Item().PaddingTop(10);

                        col.Item().Table(tabla =>
                        {
                            tabla.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f);
                            });

                            tabla.Header(header =>
                            {
                                header.Cell().Text("Producto").Bold();
                                header.Cell().Text("Cant.").Bold();
                                header.Cell().Text("P. Unit.").Bold();
                                header.Cell().Text("Subtotal").Bold();
                                header.Cell().ColumnSpan(4).PaddingTop(3).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
                            });

                            if (venta.Detalles != null)
                            {
                                foreach (var detalle in venta.Detalles)
                                {
                                    var nombreProd = detalle.Producto?.Nombre ?? detalle.NombreProducto ?? "Producto";
                                    tabla.Cell().Text(nombreProd);
                                    tabla.Cell().Text(detalle.Cantidad.ToString());
                                    tabla.Cell().Text($"Bs. {detalle.PrecioUnitario:N2}");
                                    tabla.Cell().Text($"Bs. {detalle.Subtotal:N2}");
                                }
                            }
                        });

                        col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        col.Item().AlignRight().Text($"Subtotal: Bs. {venta.Subtotal:N2}");
                        col.Item().AlignRight().Text($"Descuento: Bs. {venta.Descuento:N2}");
                        col.Item().AlignRight().Text($"TOTAL: Bs. {venta.Total:N2}").FontSize(13).Bold().FontColor("#1F3D2B");
                    });

                    pagina.Footer().AlignCenter().Text("Gracias por su compra")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });

            return pdfDocument.GeneratePdf();
        }
    }
}