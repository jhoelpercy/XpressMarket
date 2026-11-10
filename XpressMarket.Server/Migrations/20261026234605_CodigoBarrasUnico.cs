using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XpressMarket.Server.Migrations
{
    /// <inheritdoc />
    public partial class CodigoBarrasUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los códigos vacíos pasan a NULL (producto sin código) para que no choquen con el índice único.
            // Si la base ya tiene dos productos con el mismo código, esta migración falla: corrígelos antes de aplicarla.
            migrationBuilder.Sql("UPDATE Productos SET CodigoBarras = NULL WHERE TRIM(CodigoBarras) = '';");

            // Algunas bases (creadas antes de que el modelo tuviera este índice) no lo tienen: se borra sólo si existe.
            migrationBuilder.Sql(@"
                SET @sql = (SELECT IF(COUNT(*) > 0,
                    'ALTER TABLE `Productos` DROP INDEX `IX_Productos_CodigoBarras`', 'SELECT 1')
                    FROM information_schema.statistics
                    WHERE table_schema = DATABASE() AND table_name = 'Productos' AND index_name = 'IX_Productos_CodigoBarras');
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CodigoBarras",
                table: "Productos",
                column: "CodigoBarras",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Productos_CodigoBarras",
                table: "Productos");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CodigoBarras",
                table: "Productos",
                column: "CodigoBarras");
        }
    }
}
