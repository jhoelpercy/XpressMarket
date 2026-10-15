using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XpressMarket.Server.Migrations
{
    /// <inheritdoc />
    public partial class IndicesRendimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Ventas_Estado_FechaVenta",
                table: "Ventas",
                columns: new[] { "Estado", "FechaVenta" });

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_FechaVenta",
                table: "Ventas",
                column: "FechaVenta");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Activo_Nombre",
                table: "Productos",
                columns: new[] { "Activo", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_Activo_FechaVencimiento",
                table: "Lotes",
                columns: new[] { "Activo", "FechaVencimiento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ventas_Estado_FechaVenta",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_FechaVenta",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_Productos_Activo_Nombre",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Lotes_Activo_FechaVencimiento",
                table: "Lotes");
        }
    }
}
