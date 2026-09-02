using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace XpressMarket.Shared.Models
{
    public class DetalleVenta
    {
        [Key]
        public int Id { get; set; }

        // Clave foránea hacia Venta
        [Required]
        [ForeignKey(nameof(Venta))]
        public int VentaId { get; set; }

        [JsonIgnore]
        public Venta? Venta { get; set; }

        // Clave foránea hacia Producto
        [Required(ErrorMessage = "Debe especificar el producto.")]
        [ForeignKey(nameof(Producto))]
        public int ProductoId { get; set; }

        public Producto? Producto { get; set; }

        // Se guarda de forma desnormalizada para conservar el nombre histórico del producto
        [Required]
        [StringLength(150)]
        public string NombreProducto { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Cantidad { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999.99, ErrorMessage = "El precio unitario debe ser mayor a 0.")]
        public decimal PrecioUnitario { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 999999.99, ErrorMessage = "El descuento no puede ser negativo.")]
        public decimal Descuento { get; set; } = 0;

        // Propiedad calculada: (Cantidad * PrecioUnitario) - Descuento
        [NotMapped]
        public decimal Subtotal => (Cantidad * PrecioUnitario) - Descuento;
    }
}