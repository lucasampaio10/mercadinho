using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PDV.Domain.Entities;

namespace PDV.Infrastructure.Persistence.Configurations;

public sealed class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("produtos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Categoria).HasMaxLength(100);
        builder.Property(p => p.CodigoBarras).HasMaxLength(64);
        builder.Property(p => p.Preco).HasPrecision(10, 2);
        builder.HasIndex(p => p.Nome);
        builder.HasIndex(p => p.CodigoBarras);

        // Token de concorrência otimista. xmin é coluna de sistema do PostgreSQL —
        // nenhuma coluna é criada, mas o EF passa a incluí-la no WHERE do UPDATE.
        // É o que impede dois caixas de venderem a mesma unidade de estoque.
        builder.UseXminAsConcurrencyToken();
    }
}

public sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("clientes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Telefone).HasMaxLength(20);
        builder.Property(c => c.SaldoFiado).HasPrecision(10, 2);

        builder.HasMany(c => c.HistoricoFiado)
               .WithOne()
               .HasForeignKey(f => f.ClienteId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Pagamentos)
               .WithOne()
               .HasForeignKey(p => p.ClienteId)
               .OnDelete(DeleteBehavior.Cascade);

        // Protege o saldo de fiado contra dois pagamentos simultâneos do mesmo cliente.
        builder.UseXminAsConcurrencyToken();
    }
}

public sealed class VendaConfiguration : IEntityTypeConfiguration<Venda>
{
    public void Configure(EntityTypeBuilder<Venda> builder)
    {
        builder.ToTable("vendas");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.FormaPagamento).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.Observacao).HasMaxLength(500);

        builder.HasMany(v => v.Itens)
               .WithOne()
               .HasForeignKey(i => i.VendaId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ItemVendaConfiguration : IEntityTypeConfiguration<ItemVenda>
{
    public void Configure(EntityTypeBuilder<ItemVenda> builder)
    {
        builder.ToTable("itens_venda");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.NomeProduto).IsRequired().HasMaxLength(200);
        builder.Property(i => i.PrecoUnitario).HasPrecision(10, 2);
    }
}

public sealed class ItemFiadoConfiguration : IEntityTypeConfiguration<ItemFiado>
{
    public void Configure(EntityTypeBuilder<ItemFiado> builder)
    {
        builder.ToTable("itens_fiado");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Valor).HasPrecision(10, 2);
        builder.Property(i => i.ValorPago).HasPrecision(10, 2);
        builder.HasIndex(i => i.ClienteId);
    }
}

public sealed class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("pagamentos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Valor).HasPrecision(10, 2);
        builder.HasIndex(p => p.ClienteId);
    }
}
