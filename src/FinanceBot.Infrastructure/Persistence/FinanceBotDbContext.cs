using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Domain.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceBot.Infrastructure.Persistence;

public sealed class FinanceBotDbContext : DbContext
{
    public FinanceBotDbContext(DbContextOptions<FinanceBotDbContext> options)
        : base(options)
    {
    }

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<CategoryLimit> CategoryLimits => Set<CategoryLimit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigurePurchase(modelBuilder.Entity<Expense>());
        ConfigureCategoryLimit(modelBuilder.Entity<CategoryLimit>());
    }

    private static void ConfigurePurchase(EntityTypeBuilder<Expense> expenseConfiguration)
    {
        expenseConfiguration.ToTable("compras");
        expenseConfiguration.HasKey(expense => expense.Id);

        expenseConfiguration.Property(expense => expense.Id)
            .HasColumnName("id");

        expenseConfiguration.Property(expense => expense.Amount)
            .HasColumnName("valor")
            .IsRequired();

        expenseConfiguration.Property(expense => expense.Description)
            .HasColumnName("descricao")
            .HasMaxLength(255)
            .IsRequired();

        expenseConfiguration.Property(expense => expense.Category)
            .HasColumnName("categoria")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        expenseConfiguration.Property(expense => expense.DateUtc)
            .HasColumnName("data")
            .IsRequired();
    }

    private static void ConfigureCategoryLimit(EntityTypeBuilder<CategoryLimit> cfg)
    {
        cfg.ToTable("limite_categorias");
        cfg.HasKey(l => l.Id);

        cfg.Property(l => l.Id)
            .HasColumnName("id");

        cfg.Property(l => l.Category)
            .HasColumnName("categoria")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        cfg.HasIndex(l => l.Category)
            .IsUnique();

        cfg.Property(l => l.Amount)
            .HasColumnName("valor")
            .IsRequired();
    }
}
