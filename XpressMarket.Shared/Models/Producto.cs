using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace XpressMarket.Shared.Models
{
    public class Producto
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 150 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(300, ErrorMessage = "La descripción no puede superar los 300 caracteres.")]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "El precio de venta es obligatorio.")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999.99, ErrorMessage = "El precio de venta debe ser mayor a 0.")]
        public decimal PrecioVenta { get; set; }

        [Required(ErrorMessage = "El precio de costo es obligatorio.")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999.99, ErrorMessage = "El precio de costo debe ser mayor a 0.")]
        public decimal PrecioCosto { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "El stock actual no puede ser negativo.")]
        public int StockActual { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")]
        public int StockMinimo { get; set; }

        [StringLength(50, ErrorMessage = "El código de barras no puede superar los 50 caracteres.")]
        public string? CodigoBarras { get; set; }

        [Required]
        public bool Activo { get; set; } = true;

        [Required]
        [DataType(DataType.Date)]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Clave foránea hacia Categoria
        [Required(ErrorMessage = "Debe seleccionar una categoría.")]
        [ForeignKey(nameof(Categoria))]
        public int CategoriaId { get; set; }

        [JsonIgnore]
        public Categoria? Categoria { get; set; }

        // Propiedad calculada: indica si el stock actual está por debajo o igual al mínimo
        [NotMapped]
        public bool StockBajo => StockActual <= StockMinimo;

        [JsonIgnore]
        public ICollection<Lote> Lotes { get; set; } = new List<Lote>();
    }
}