
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using XpressMarket.Shared.Models;

namespace XpressMarket.Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Categoria> Categorias { get; set; } = null!;
        public DbSet<Producto> Productos { get; set; } = null!;
        public DbSet<Venta> Ventas { get; set; } = null!;
        public DbSet<DetalleVenta> DetallesVenta { get; set; } = null!;
        public DbSet<Proveedor> Proveedores { get; set; } = null!;
        public DbSet<Lote> Lotes { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Relación Categoria (1) - Producto (N)
            // Restrict: no permite eliminar una categoría si tiene productos asociados
            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Categoria)
                .WithMany(c => c.Productos)
                .HasForeignKey(p => p.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relación Venta (1) - DetalleVenta (N)
            // Cascade: al eliminar una venta se eliminan automáticamente sus detalles
            modelBuilder.Entity<DetalleVenta>()
                .HasOne(d => d.Venta)
                .WithMany(v => v.Detalles)
                .HasForeignKey(d => d.VentaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación Producto (1) - DetalleVenta (N)
            // Restrict: evita eliminar un producto si ya tiene ventas registradas (protege el histórico)
            modelBuilder.Entity<DetalleVenta>()
                .HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Índices útiles
            modelBuilder.Entity<Producto>()
                .HasIndex(p => p.CodigoBarras)
                .IsUnique(false);

            modelBuilder.Entity<Categoria>()
                .HasIndex(c => c.Nombre)
                .IsUnique();

            // Relación Producto (1) - Lote (N)
            modelBuilder.Entity<Lote>()
                .HasOne(l => l.Producto)
                .WithMany(p => p.Lotes)
                .HasForeignKey(l => l.ProductoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación Proveedor (1) - Lote (N), opcional
            modelBuilder.Entity<Lote>()
                .HasOne(l => l.Proveedor)
                .WithMany(pr => pr.Lotes)
                .HasForeignKey(l => l.ProveedorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Proveedor>()
                .HasIndex(p => p.Nombre);

            modelBuilder.Entity<Lote>()
                .HasIndex(l => l.FechaVencimiento);
        }
    }
}