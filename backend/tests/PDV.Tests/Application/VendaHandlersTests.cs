using PDV.Application.Commands.Vendas;
using PDV.Domain.Entities;
using PDV.Tests.Fakes;

namespace PDV.Tests.Application;

public class VendaHandlersTests
{
    private readonly ProdutoRepositoryFake _produtos = new();
    private readonly VendaRepositoryFake _vendas = new();
    private readonly ClienteRepositoryFake _clientes = new();
    private readonly UnitOfWorkFake _uow = new();

    private Produto DadoUmProduto(decimal preco = 10m, int estoque = 100)
    {
        var produto = Produto.Criar("Coca-Cola 2L", preco, estoque);
        _produtos.Itens.Add(produto);
        return produto;
    }

    private Venda DadaUmaVendaCom(Produto produto, int quantidade)
    {
        var venda = Venda.Iniciar();
        venda.AdicionarItem(produto, quantidade);
        _vendas.Itens.Add(venda);
        return venda;
    }

    // ── Adicionar item ────────────────────────────────────────────────────────

    [Fact]
    public async Task AdicionarItem_ProdutoInexistente_Falha()
    {
        var venda = Venda.Iniciar();
        _vendas.Itens.Add(venda);

        var handler = new AdicionarItemHandler(_vendas, _produtos, _uow);
        var result = await handler.Handle(
            new AdicionarItemCommand(venda.Id, Guid.NewGuid(), 1), default);

        Assert.True(result.IsFailure);
        Assert.Equal(0, _uow.Commits);
    }

    [Fact]
    public async Task AdicionarItem_ConfereEstoqueContraOTotalDoCarrinho()
    {
        var produto = DadoUmProduto(estoque: 5);
        var venda = DadaUmaVendaCom(produto, 3);

        var handler = new AdicionarItemHandler(_vendas, _produtos, _uow);

        // 3 já no carrinho + 3 = 6 > 5 disponíveis
        var result = await handler.Handle(new AdicionarItemCommand(venda.Id, produto.Id, 3), default);

        Assert.True(result.IsFailure);
        Assert.Contains("Estoque insuficiente", result.Error);
    }

    [Fact]
    public async Task AdicionarItem_NaoDebitaEstoqueAntesDaFinalizacao()
    {
        var produto = DadoUmProduto(estoque: 10);
        var venda = Venda.Iniciar();
        _vendas.Itens.Add(venda);

        var handler = new AdicionarItemHandler(_vendas, _produtos, _uow);
        var result = await handler.Handle(new AdicionarItemCommand(venda.Id, produto.Id, 4), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, produto.Estoque);
    }

    // ── Alterar quantidade ────────────────────────────────────────────────────

    [Fact]
    public async Task AlterarQuantidade_CorrigeOItemSemRemoverERefazer()
    {
        var produto = DadoUmProduto();
        var venda = DadaUmaVendaCom(produto, 10);

        var handler = new AlterarQuantidadeItemHandler(_vendas, _produtos, _uow);
        var result = await handler.Handle(
            new AlterarQuantidadeItemCommand(venda.Id, produto.Id, 1), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, venda.Itens.Single().Quantidade);
        Assert.Equal(1, _uow.Commits);
    }

    [Fact]
    public async Task AlterarQuantidade_AcimaDoEstoque_FalhaSemGravar()
    {
        var produto = DadoUmProduto(estoque: 5);
        var venda = DadaUmaVendaCom(produto, 2);

        var handler = new AlterarQuantidadeItemHandler(_vendas, _produtos, _uow);
        var result = await handler.Handle(
            new AlterarQuantidadeItemCommand(venda.Id, produto.Id, 6), default);

        Assert.True(result.IsFailure);
        Assert.Equal(2, venda.Itens.Single().Quantidade);
        Assert.Equal(0, _uow.Commits);
    }

    [Fact]
    public async Task AlterarQuantidade_VendaInexistente_Falha()
    {
        var handler = new AlterarQuantidadeItemHandler(_vendas, _produtos, _uow);
        var result = await handler.Handle(
            new AlterarQuantidadeItemCommand(Guid.NewGuid(), Guid.NewGuid(), 1), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Venda não encontrada.", result.Error);
    }

    // ── Finalizar ─────────────────────────────────────────────────────────────

    private FinalizarVendaHandler Finalizador() =>
        new(_vendas, _produtos, _clientes, _uow);

    [Theory]
    [InlineData("99")]
    [InlineData("-1")]
    [InlineData("2147483647")]
    [InlineData("Pix")]
    [InlineData("")]
    public async Task Finalizar_FormaDePagamentoQueNaoExiste_Falha(string forma)
    {
        var venda = DadaUmaVendaCom(DadoUmProduto(), 1);

        // Enum.TryParse sozinho aceita qualquer número: "99" viraria uma
        // FormaPagamento 99 e seria gravada como a string "99" no banco.
        var result = await Finalizador().Handle(
            new FinalizarVendaCommand(venda.Id, forma, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Forma de pagamento inválida.", result.Error);
        Assert.Equal(StatusVenda.Aberta, venda.Status);
        Assert.Equal(0, _uow.Commits);
    }

    [Theory]
    [InlineData("Dinheiro")]
    [InlineData("dinheiro")]
    [InlineData("CARTAO")]
    public async Task Finalizar_NomeDaFormaEmQualquerCaixa_Aceita(string forma)
    {
        var venda = DadaUmaVendaCom(DadoUmProduto(), 1);

        var result = await Finalizador().Handle(
            new FinalizarVendaCommand(venda.Id, forma, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusVenda.Finalizada, venda.Status);
    }

    [Fact]
    public async Task Finalizar_ComClienteSemSerFiado_Falha()
    {
        var venda = DadaUmaVendaCom(DadoUmProduto(), 1);
        var cliente = Cliente.Criar("Dona Maria");
        _clientes.Itens.Add(cliente);

        var result = await Finalizador().Handle(
            new FinalizarVendaCommand(venda.Id, "Dinheiro", cliente.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal(StatusVenda.Aberta, venda.Status);
        Assert.Equal(0m, cliente.SaldoFiado);
        Assert.Equal(0, _uow.Commits);
    }

    [Fact]
    public async Task Finalizar_DebitaOEstoqueDosItens()
    {
        var produto = DadoUmProduto(estoque: 10);
        var venda = DadaUmaVendaCom(produto, 3);

        var result = await Finalizador().Handle(
            new FinalizarVendaCommand(venda.Id, "Dinheiro", null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, produto.Estoque);
    }

    [Fact]
    public async Task Finalizar_Fiado_LancaNoHistoricoDoCliente()
    {
        var produto = DadoUmProduto(preco: 12.50m);
        var venda = DadaUmaVendaCom(produto, 2);
        var cliente = Cliente.Criar("Dona Maria");
        _clientes.Itens.Add(cliente);

        var result = await Finalizador().Handle(
            new FinalizarVendaCommand(venda.Id, "Fiado", cliente.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(25m, cliente.SaldoFiado);
        Assert.Single(_clientes.FiadosRegistrados);
        Assert.Equal(venda.Id, _clientes.FiadosRegistrados.Single().VendaId);
    }

    [Fact]
    public async Task Finalizar_FiadoComClienteInexistente_Falha()
    {
        var venda = DadaUmaVendaCom(DadoUmProduto(), 1);

        var result = await Finalizador().Handle(
            new FinalizarVendaCommand(venda.Id, "Fiado", Guid.NewGuid()), default);

        Assert.True(result.IsFailure);
        Assert.Equal(0, _uow.Commits);
    }

    [Fact]
    public async Task Finalizar_VendaSemItens_Falha()
    {
        var venda = Venda.Iniciar();
        _vendas.Itens.Add(venda);

        var result = await Finalizador().Handle(
            new FinalizarVendaCommand(venda.Id, "Dinheiro", null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(0, _uow.Commits);
    }
}
