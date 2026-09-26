using PDV.Domain.Entities;
using PDV.Domain.Exceptions;

namespace PDV.Tests.Domain;

public class ProdutoTests
{
    [Fact]
    public void Criar_PrecoZero_Recusa()
    {
        // O front já barrava preço <= 0; o domínio aceitava 0 — regras divergentes.
        var ex = Assert.Throws<DomainException>(() => Produto.Criar("Arroz", 0m, 10));
        Assert.Contains("maior que zero", ex.Message);
    }

    [Fact]
    public void Criar_PrecoNegativo_Recusa() =>
        Assert.Throws<DomainException>(() => Produto.Criar("Arroz", -1m, 10));

    [Fact]
    public void Criar_EstoqueNegativo_Recusa() =>
        Assert.Throws<DomainException>(() => Produto.Criar("Arroz", 5m, -1));

    [Fact]
    public void Criar_SemNome_Recusa() =>
        Assert.Throws<DomainException>(() => Produto.Criar("   ", 5m, 10));

    [Fact]
    public void GarantirEstoqueDisponivel_NaoAlteraOEstoque()
    {
        var produto = Produto.Criar("Arroz", 5m, 10);

        produto.GarantirEstoqueDisponivel(10);

        Assert.Equal(10, produto.Estoque);
    }

    [Fact]
    public void GarantirEstoqueDisponivel_AcimaDoQueTem_Recusa()
    {
        var produto = Produto.Criar("Arroz", 5m, 3);

        var ex = Assert.Throws<DomainException>(() => produto.GarantirEstoqueDisponivel(4));
        Assert.Contains("Estoque insuficiente", ex.Message);
    }

    [Fact]
    public void DebitarECreditar_VoltamAoEstoqueOriginal()
    {
        var produto = Produto.Criar("Arroz", 5m, 10);

        produto.DebitarEstoque(4);
        Assert.Equal(6, produto.Estoque);

        produto.CreditarEstoque(4);
        Assert.Equal(10, produto.Estoque);
    }
}

public class ClienteTests
{
    private static Cliente ComFiados(params decimal[] valores)
    {
        var cliente = Cliente.Criar("Dona Maria");
        foreach (var valor in valores)
            cliente.AdicionarFiado(Guid.NewGuid(), valor);
        return cliente;
    }

    [Fact]
    public void AdicionarFiado_SomaNoSaldo()
    {
        var cliente = ComFiados(10m, 15m);

        Assert.Equal(25m, cliente.SaldoFiado);
        Assert.Equal(2, cliente.HistoricoFiado.Count);
    }

    [Fact]
    public void RegistrarPagamento_AbateOMaisAntigoPrimeiro()
    {
        var cliente = ComFiados(10m, 15m);

        cliente.RegistrarPagamento(10m);

        var maisAntigo = cliente.HistoricoFiado.OrderBy(i => i.CreatedAt).First();
        Assert.True(maisAntigo.Pago);
        Assert.Equal(15m, cliente.SaldoFiado);
    }

    [Fact]
    public void RegistrarPagamento_Parcial_DeixaOItemEmAbertoComValorPago()
    {
        var cliente = ComFiados(10m);

        cliente.RegistrarPagamento(4m);

        var item = cliente.HistoricoFiado.Single();
        Assert.False(item.Pago);
        Assert.Equal(4m, item.ValorPago);
        Assert.Equal(6m, item.Saldo);
        Assert.Equal(6m, cliente.SaldoFiado);
    }

    [Fact]
    public void RegistrarPagamento_AcimaDoSaldo_Recusa()
    {
        var cliente = ComFiados(10m);

        Assert.Throws<DomainException>(() => cliente.RegistrarPagamento(10.01m));
    }

    [Fact]
    public void RegistrarPagamento_ValorZero_Recusa() =>
        Assert.Throws<DomainException>(() => ComFiados(10m).RegistrarPagamento(0m));
}

public class EntityTests
{
    [Fact]
    public void Equals_TiposDiferentesComMesmoId_NaoSaoIguais()
    {
        var produto = Produto.Criar("Arroz", 5m, 1);
        var cliente = Cliente.Criar("Arroz");

        // Guids são gerados por entidade, mas a igualdade não pode depender disso:
        // comparar só o Id tornaria um Produto e um Cliente intercambiáveis.
        Assert.NotEqual<object>(produto, cliente);
    }

    [Fact]
    public void Equals_MesmaInstancia_EIgual()
    {
        var produto = Produto.Criar("Arroz", 5m, 1);

        Assert.Equal(produto, produto);
        Assert.Equal(produto.GetHashCode(), produto.GetHashCode());
    }

    [Fact]
    public void Equals_MesmoTipoIdsDiferentes_NaoSaoIguais()
    {
        Assert.NotEqual(Produto.Criar("Arroz", 5m, 1), Produto.Criar("Arroz", 5m, 1));
    }
}
