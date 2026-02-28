using FinanceBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceBot.Infrastructure.Persistence;

public sealed class FinanceBotDbContext : DbContext
{
    public FinanceBotDbContext(DbContextOptions<FinanceBotDbContext> options)
        : base(options)
    {
    }

    public DbSet<Compra> Compras => Set<Compra>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureCompra(modelBuilder.Entity<Compra>());
    }

    private static void ConfigureCompra(EntityTypeBuilder<Compra> compraConfiguration)
    {
        compraConfiguration.ToTable("compras");
        compraConfiguration.HasKey(compra => compra.Id);

        compraConfiguration.Property(compra => compra.Id)
            .HasColumnName("id");

        compraConfiguration.Property(compra => compra.Valor)
            .HasColumnName("valor")
            .IsRequired();

        compraConfiguration.Property(compra => compra.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(255)
            .IsRequired();

        compraConfiguration.Property(compra => compra.Categoria)
            .HasColumnName("categoria")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        compraConfiguration.Property(compra => compra.Data)
            .HasColumnName("data")
            .IsRequired();
    }
}
