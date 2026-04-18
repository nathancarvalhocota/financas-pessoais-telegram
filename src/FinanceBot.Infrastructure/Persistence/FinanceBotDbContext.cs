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

    public DbSet<LimiteCategoria> LimiteCategorias => Set<LimiteCategoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureCompra(modelBuilder.Entity<Compra>());
        ConfigureLimiteCategoria(modelBuilder.Entity<LimiteCategoria>());
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

    private static void ConfigureLimiteCategoria(EntityTypeBuilder<LimiteCategoria> cfg)
    {
        cfg.ToTable("limite_categorias");
        cfg.HasKey(l => l.Id);

        cfg.Property(l => l.Id)
            .HasColumnName("id");

        cfg.Property(l => l.Categoria)
            .HasColumnName("categoria")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        cfg.HasIndex(l => l.Categoria)
            .IsUnique();

        cfg.Property(l => l.Valor)
            .HasColumnName("valor")
            .IsRequired();
    }
}
