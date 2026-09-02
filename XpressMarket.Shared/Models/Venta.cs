using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XpressMarket.Shared.Models
{
    public enum EstadoVenta
    {
        Completada = 1,
        Anulada = 2,
        Pendiente = 3
    }

    public class Venta
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime FechaVenta { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 9999999.99, ErrorMessage = "El subtotal no puede ser negativo.")]
        public decimal Subtotal { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 9999999.99, ErrorMessage = "El descuento no puede ser negativo.")]
        public decimal Descuento { get; set; } = 0;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 9999999.99, ErrorMessage = "El total no puede ser negativo.")]
        public decimal Total { get; set; }

        [StringLength(250, ErrorMessage = "La observación no puede superar los 250 caracteres.")]
        public string? Observacion { get; set; }

        [Required]
        public EstadoVenta Estado { get; set; } = EstadoVenta.Completada;

        // Referencia al usuario que registró la venta (sin FK a Identity en este sprint)
        [Required(ErrorMessage = "El usuario que registra la venta es obligatorio.")]
        public string UsuarioId { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string NombreUsuario { get; set; } = string.Empty;

        // Navegación: una Venta tiene muchos DetalleVenta
        public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
    }
}
