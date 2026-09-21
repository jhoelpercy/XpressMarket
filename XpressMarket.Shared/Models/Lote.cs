using System;
using System.Collections.Generic;
using System.Text;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace XpressMarket.Shared.Models
{
    public class Lote
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50, ErrorMessage = "El número de lote no puede superar los 50 caracteres.")]
        public string NumeroLote { get; set; } = string.Empty;

        // Clave foránea hacia Producto
        [Required(ErrorMessage = "Debe especificar el producto del lote.")]
        [ForeignKey(nameof(Producto))]
        public int ProductoId { get; set; }

        [JsonIgnore]
        public Producto? Producto { get; set; }

        // Clave foránea hacia Proveedor (opcional: puede no conocerse en algunos casos)
        [ForeignKey(nameof(Proveedor))]
        public int? ProveedorId { get; set; }

        public Proveedor? Proveedor { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime FechaIngreso { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "La fecha de vencimiento es obligatoria.")]
        [DataType(DataType.Date)]
        public DateTime FechaVencimiento { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad inicial debe ser mayor a 0.")]
        public int CantidadInicial { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "La cantidad actual no puede ser negativa.")]
        public int CantidadActual { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999.99, ErrorMessage = "El precio de costo debe ser mayor a 0.")]
        public decimal PrecioCosto { get; set; }

        [Required]
        public bool Activo { get; set; } = true;
        public bool EsMerma { get; set; } = false;
        public DateTime? FechaMerma { get; set; }

        // Propiedades calculadas para alertas de vencimiento
        [NotMapped]
        public bool Vencido => FechaVencimiento.Date < DateTime.Now.Date;

        [NotMapped]
        public int DiasParaVencer => (FechaVencimiento.Date - DateTime.Now.Date).Days;

        [NotMapped]
        public bool ProximoAVencer => !Vencido && DiasParaVencer <= 30;

    }
}