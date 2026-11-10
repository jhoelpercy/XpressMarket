using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XpressMarket.Server.Migrations
{
    /// <inheritdoc />
    public partial class AlertasNombreUnicoCostoVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AlertaStockSilenciadaHasta",
                table: "Productos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AlertaVencimientoSilenciadaHasta",
                table: "Lotes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitario",
                table: "DetallesVenta",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            // Costo de las ventas ya registradas: el promedio del costo de los lotes que consumieron (FEFO) y, para
            // ventas sin consumos, el costo actual del producto. Desde ahora cada venta nueva guarda su propio costo.
            migrationBuilder.Sql(@"
                UPDATE DetallesVenta d
                SET d.CostoUnitario = COALESCE(
                    (SELECT SUM(c.Cantidad * l.PrecioCosto) / NULLIF(SUM(c.Cantidad), 0)
                       FROM Consumos c JOIN Lotes l ON l.Id = c.LoteId
                      WHERE c.DetalleVentaId = d.Id),
                    (SELECT p.PrecioCosto FROM Productos p WHERE p.Id = d.ProductoId),
                    0);");

            // Si ya había productos con el mismo nombre, se renombran (Nombre, Nombre (2), ...) para poder crear el
            // índice único. Se conserva el de menor Id con su nombre original; revisa después los renombrados.
            migrationBuilder.Sql(@"
                UPDATE Productos p
                JOIN (SELECT Id, ROW_NUMBER() OVER (PARTITION BY Nombre ORDER BY Id) AS n FROM Productos) d ON d.Id = p.Id
                SET p.Nombre = CONCAT(LEFT(p.Nombre, 140), ' (', d.n, ')')
                WHERE d.n > 1;");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Nombre",
                table: "Productos",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Productos_Nombre",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "AlertaStockSilenciadaHasta",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "AlertaVencimientoSilenciadaHasta",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "CostoUnitario",
                table: "DetallesVenta");
        }
    }
}
