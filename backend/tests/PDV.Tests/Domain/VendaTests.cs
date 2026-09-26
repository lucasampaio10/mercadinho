using PDV.Domain.Entities;
using PDV.Domain.Exceptions;

namespace PDV.Tests.Domain;

public class VendaTests
{
    private static Produto UmProduto(decimal preco = 10m, int estoque = 100) =>
        Produto.Criar("Coca-Cola 2L", preco, estoque);

    private static Venda UmaVendaCom(Produto produto, int quantidade = 1)
    {
        var venda = Venda.Iniciar();
        venda.AdicionarItem(produto, quantidade);
        return venda;
    }

    [Fact]
    public void AdicionarItem_MesmoProduto_IncrementaEmVezDeDuplicar()
    {
        var produto = UmProduto();
        var venda = UmaVendaCom(produto, 2);

        var novoItem = venda.AdicionarItem(produto, 3);

        Assert.Null(novoItem); // já existia: incrementou
        Assert.Single(venda.Itens);
        Assert.Equal(5, venda.Itens.Single().Quantidade);
    }

    [Fact]
    public void AdicionarItem_UsaPrecoDoMomentoDaVenda()
    {
        var produto = UmProduto(preco: 8m);
        var venda = UmaVendaCom(produto, 2);

        produto.Atualizar(produto.Nome, preco: 99m, produto.Estoque, produto.Categoria);

        Assert.Equal(16m, venda.Total);
    }

    [Fact]
    public void AdicionarItem_AcimaDoTeto_Recusa()
    {
        var produto = UmProduto(estoque: int.MaxValue);
        var venda = Venda.Iniciar();

        var ex = Assert.Throws<DomainException>(() => venda.AdicionarItem(produto, int.MaxValue));
        Assert.Contains("Quantidade máxima", ex.Message);
    }

    [Fact]
    public void AdicionarItem_IncrementoQueEstouraOTeto_Recusa()
    {
        var produto = UmProduto();
        var venda = UmaVendaCom(produto, ItemVenda.QuantidadeMaxima);

        Assert.Throws<DomainException>(() => venda.AdicionarItem(produto, 1));
    }

    // ── Alterar quantidade ────────────────────────────────────────────────────

    [Fact]
    public void AlterarQuantidadeItem_DefineOValorExatoEDevolveADiferenca()
    {
        var produto = UmProduto();
        var venda = UmaVendaCom(produto, 10);

        var delta = venda.AlterarQuantidadeItem(produto.Id, 1);

        Assert.Equal(-9, delta);
        Assert.Equal(1, venda.Itens.Single().Quantidade);
    }

    [Fact]
    public void AlterarQuantidadeItem_ParaCima_DevolveDeltaPositivo()
    {
        var produto = UmProduto();
        var venda = UmaVendaCom(produto, 2);

        Assert.Equal(3, venda.AlterarQuantidadeItem(produto.Id, 5));
    }

    [Fact]
    public void AlterarQuantidadeItem_ParaZero_Recusa()
    {
        var produto = UmProduto();
        var venda = UmaVendaCom(produto, 2);

        Assert.Throws<DomainException>(() => venda.AlterarQuantidadeItem(produto.Id, 0));
    }

    [Fact]
    public void AlterarQuantidadeItem_ProdutoForaDoCarrinho_Recusa()
    {
        var venda = UmaVendaCom(UmProduto(), 1);

        Assert.Throws<DomainException>(() => venda.AlterarQuantidadeItem(Guid.NewGuid(), 3));
    }

    [Fact]
    public void AlterarQuantidadeItem_VendaFinalizada_Recusa()
    {
        var produto = UmProduto();
        var venda = UmaVendaCom(produto, 1);
        venda.Finalizar(FormaPagamento.Dinheiro);

        Assert.Throws<DomainException>(() => venda.AlterarQuantidadeItem(produto.Id, 2));
    }

    // ── Remover ───────────────────────────────────────────────────────────────

    [Fact]
    public void RemoverItem_DevolveOItemRemovido()
    {
        var produto = UmProduto();
        var venda = UmaVendaCom(produto, 4);

        var removido = venda.RemoverItem(produto.Id);

        Assert.Equal(4, removido.Quantidade);
        Assert.Empty(venda.Itens);
    }

    // ── Finalizar ─────────────────────────────────────────────────────────────

    [Fact]
    public void Finalizar_FiadoSemCliente_Recusa()
    {
        var venda = UmaVendaCom(UmProduto());

        var ex = Assert.Throws<DomainException>(() => venda.Finalizar(FormaPagamento.Fiado, null));
        Assert.Contains("Cliente é obrigatório", ex.Message);
    }

    [Theory]
    [InlineData(FormaPagamento.Dinheiro)]
    [InlineData(FormaPagamento.Cartao)]
    public void Finalizar_ComClienteSemSerFiado_Recusa(FormaPagamento forma)
    {
        var venda = UmaVendaCom(UmProduto());

        // Sem esta regra, a venda gravaria um cliente sem lançar fiado nenhum.
        var ex = Assert.Throws<DomainException>(() => venda.Finalizar(forma, Guid.NewGuid()));
        Assert.Contains("só pode ser vinculado", ex.Message);

        Assert.Equal(StatusVenda.Aberta, venda.Status);
        Assert.Null(venda.ClienteId);
    }

    [Fact]
    public void Finalizar_FiadoComCliente_VinculaOCliente()
    {
        var clienteId = Guid.NewGuid();
        var venda = UmaVendaCom(UmProduto());

        venda.Finalizar(FormaPagamento.Fiado, clienteId);

        Assert.Equal(StatusVenda.Finalizada, venda.Status);
        Assert.Equal(clienteId, venda.ClienteId);
    }

    [Fact]
    public void Finalizar_SemItens_Recusa()
    {
        var venda = Venda.Iniciar();

        Assert.Throws<DomainException>(() => venda.Finalizar(FormaPagamento.Dinheiro));
    }

    [Fact]
    public void Finalizar_DuasVezes_Recusa()
    {
        var venda = UmaVendaCom(UmProduto());
        venda.Finalizar(FormaPagamento.Dinheiro);

        Assert.Throws<DomainException>(() => venda.Finalizar(FormaPagamento.Cartao));
    }
}
