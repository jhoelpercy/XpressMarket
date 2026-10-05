using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using XpressMarket.Shared.Models;

namespace XpressMarket.Server.Services
{
    public class TicketPdfService
    {
        public static byte[] Generar(Venta venta)
        {
            // Calcular la altura dinámica según los productos
            int itemsCount = venta.Detalles?.Count ?? 0;
            float alturaMm = 70 + (itemsCount * 5);

            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(80, alturaMm, Unit.Millimetre);
                    page.Margin(3, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(7).FontFamily("Lato"));

                    page.Content().Column(col =>
                    {
                        // Encabezado
                        col.Item().AlignCenter().Text("XPRESSMARKET").Bold().FontSize(9);
                        col.Item().AlignCenter().Text("Supermercado y Minimarket").FontSize(6.5f);
                        col.Item().AlignCenter().Text("NIT: 1234567890123 - Sucursal N° 1").FontSize(6.5f);
                        col.Item().AlignCenter().Text("La Paz, Bolivia").FontSize(6.5f);
                        col.Item().AlignCenter().Text("------------------------------------------").FontSize(6.5f);

                        // Datos Generales
                        col.Item().Text($"N° Ticket: {venta.Id:D6}").FontSize(7);
                        col.Item().Text($"Fecha: {venta.FechaVenta:dd/MM/yyyy HH:mm}").FontSize(7);
                        col.Item().Text($"Cajero(a): {venta.NombreUsuario}").FontSize(7);
                        col.Item().Text($"Estado: {venta.Estado}").FontSize(7);
                        col.Item().AlignCenter().Text("------------------------------------------").FontSize(6.5f);

                        // Detalle de Productos
                        col.Item().Text("DETALLE DE PRODUCTOS").Bold().FontSize(7);
                        col.Item().AlignCenter().Text("------------------------------------------").FontSize(6.5f);

                        foreach (var item in venta.Detalles)
                        {
                            col.Item().Text($"{item.Cantidad}x {item.NombreProducto}").FontSize(7);
                            col.Item().AlignRight().Text($"Subtotal: Bs. {item.Subtotal:N2}").FontSize(7);
                            if (item.Descuento > 0)
                            {
                                col.Item().AlignRight().Text($"Desc: -Bs. {item.Descuento:N2}").FontSize(6.5f);
                            }
                        }

                        col.Item().AlignCenter().Text("------------------------------------------").FontSize(6.5f);

                        // Totales
                        col.Item().AlignRight().Text($"Subtotal: Bs. {venta.Subtotal:N2}").FontSize(7);
                        if (venta.Descuento > 0)
                        {
                            col.Item().AlignRight().Text($"Descuentos: -Bs. {venta.Descuento:N2}").FontSize(7);
                        }
                        col.Item().AlignRight().Text($"TOTAL A PAGAR: Bs. {venta.Total:N2}").Bold().FontSize(8);

                        col.Item().AlignCenter().Text("------------------------------------------").FontSize(6.5f);
                        col.Item().AlignCenter().Text("¡Gracias por su compra en XpressMarket!").FontSize(6.5f);
                        col.Item().AlignCenter().Text("Conserve este ticket para cualquier cambio").FontSize(6);
                        col.Item().AlignCenter().Text("o devolución dentro de los 5 días hábiles.").FontSize(6);
                    });
                });
            });

            return documento.GeneratePdf();
        }
    }
}