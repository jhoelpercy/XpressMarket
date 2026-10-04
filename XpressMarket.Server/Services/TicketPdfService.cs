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
            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(new PageSize(226, 242));
                    page.Margin(8);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Lato"));

                    page.Content().Column(col =>
                    {
                        // Encabezado
                        col.Item().AlignCenter().Text("XPRESSMARKET").Bold().FontSize(11);
                        col.Item().AlignCenter().Text("Supermercado y Minimarket");
                        col.Item().AlignCenter().Text("NIT: 1234567890123 - Sucursal N° 1");
                        col.Item().AlignCenter().Text("La Paz, Bolivia");
                        col.Item().AlignCenter().Text("--------------------------------------------------");

                        // Datos Generales
                        col.Item().Text($"N° Ticket: {venta.Id:D6}");
                        col.Item().Text($"Fecha: {venta.FechaVenta:dd/MM/yyyy HH:mm}");
                        col.Item().Text($"Cajero(a): {venta.NombreUsuario}");
                        col.Item().Text($"Estado: {venta.Estado}");
                        col.Item().AlignCenter().Text("--------------------------------------------------");

                        // Detalle de Productos
                        col.Item().Text("DETALLE DE PRODUCTOS").Bold();
                        col.Item().AlignCenter().Text("--------------------------------------------------");

                        foreach (var item in venta.Detalles)
                        {
                            col.Item().Text($"{item.Cantidad}x {item.NombreProducto}");
                            col.Item().AlignRight().Text($"Subtotal: Bs. {item.Subtotal:N2}");
                            if (item.Descuento > 0)
                            {
                                col.Item().AlignRight().Text($"Desc: -Bs. {item.Descuento:N2}");
                            }
                        }

                        col.Item().AlignCenter().Text("--------------------------------------------------");

                        // Totales
                        col.Item().AlignRight().Text($"Subtotal: Bs. {venta.Subtotal:N2}");
                        if (venta.Descuento > 0)
                        {
                            col.Item().AlignRight().Text($"Descuentos: -Bs. {venta.Descuento:N2}");
                        }
                        col.Item().AlignRight().Text($"TOTAL A PAGAR: Bs. {venta.Total:N2}").Bold().FontSize(9);

                        col.Item().AlignCenter().Text("--------------------------------------------------");
                        col.Item().AlignCenter().Text("¡Gracias por su compra en XpressMarket!");
                        col.Item().AlignCenter().Text("Conserve este ticket para cualquier cambio o devolución dentro de los 5 días hábiles.");
                    });
                });
            });

            return documento.GeneratePdf();
        }
    }
}