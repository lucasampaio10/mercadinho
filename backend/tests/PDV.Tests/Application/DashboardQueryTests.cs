using PDV.Application.Commands.Clientes;
using PDV.Application.Common;
using PDV.Application.Queries;
using PDV.Domain.Entities;
using PDV.Tests.Fakes;

namespace PDV.Tests.Application;

public class DashboardQueryTests
{
    private readonly VendaRepositoryFake _vendas = new();
    private readonly ClienteRepositoryFake _clientes = new();

    private void DadaUmaVendaFinalizada(FormaPagamento forma, decimal preco, int quantidade, Guid? clienteId = null)
    {
        var venda = Venda.Iniciar();
        venda.AdicionarItem(Produto.Criar("Item", preco, 1_000), quantidade);
        venda.Finalizar(forma, clienteId);
        _vendas.Itens.Add(venda);
    }

    [Fact]
    public async Task Dashboard_SeparaOQueVirouCaixaDoQueSaiuFiado()
    {
        var cliente = Cliente.Criar("Dona Maria");
        _clientes.Itens.Add(cliente);

        DadaUmaVendaFinalizada(FormaPagamento.Dinheiro, 10m, 2);  // 20
        DadaUmaVendaFinalizada(FormaPagamento.Cartao, 5m, 3);     // 15
        DadaUmaVendaFinalizada(FormaPagamento.Fiado, 8m, 5, cliente.Id); // 40

        var dashboard = await new DashboardQuery(_vendas, _clientes).Handle(default);

        Assert.Equal(75m, dashboard.TotalVendidoHoje);
        Assert.Equal(35m, dashboard.TotalRecebidoHoje);
        Assert.Equal(40m, dashboard.TotalFiadoHoje);
        Assert.Equal(3, dashboard.TotalVendasHoje);
    }

    [Fact]
    public async Task Dashboard_SemVendas_ZeraOsTotais()
    {
        var dashboard = await new DashboardQuery(_vendas, _clientes).Handle(default);

        Assert.Equal(0m, dashboard.TotalVendidoHoje);
        Assert.Equal(0m, dashboard.TotalRecebidoHoje);
        Assert.Equal(0m, dashboard.TotalFiadoHoje);
        Assert.Equal(0, dashboard.TotalVendasHoje);
    }

    [Fact]
    public async Task Dashboard_SomaOSaldoDevedorDeTodosOsClientes()
    {
        foreach (var valor in new[] { 30m, 20m })
        {
            var cliente = Cliente.Criar($"Cliente {valor}");
            cliente.AdicionarFiado(Guid.NewGuid(), valor);
            _clientes.Itens.Add(cliente);
        }

        var dashboard = await new DashboardQuery(_vendas, _clientes).Handle(default);

        Assert.Equal(50m, dashboard.TotalFiadoPendente);
        Assert.Equal(2, dashboard.ClientesComFiado);
    }
}

public class FusoLocalTests
{
    [Fact]
    public void IntervaloDeHoje_CobreExatamenteVinteEQuatroHoras()
    {
        var (inicio, fim) = FusoLocal.IntervaloDeHojeUtc();

        Assert.Equal(TimeSpan.FromDays(1), fim - inicio);
        Assert.Equal(DateTimeKind.Utc, inicio.Kind);
    }

    [Fact]
    public void IntervaloDeHoje_ContemOAgoraLocalConvertidoParaUtc()
    {
        var (inicio, fim) = FusoLocal.IntervaloDeHojeUtc();
        var agora = DateTime.UtcNow;

        Assert.InRange(agora, inicio, fim);
    }
}

public class ClienteHandlersTests
{
    private readonly ClienteRepositoryFake _clientes = new();
    private readonly UnitOfWorkFake _uow = new();

    [Fact]
    public async Task RegistrarPagamento_RegistraOPagamentoEAbateOSaldo()
    {
        var cliente = Cliente.Criar("Dona Maria");
        cliente.AdicionarFiado(Guid.NewGuid(), 50m);
        _clientes.Itens.Add(cliente);

        var handler = new RegistrarPagamentoFiadoHandler(_clientes, _uow);
        var result = await handler.Handle(new RegistrarPagamentoFiadoCommand(cliente.Id, 20m), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(30m, cliente.SaldoFiado);
        Assert.Single(_clientes.PagamentosRegistrados);
        Assert.Equal(20m, _clientes.PagamentosRegistrados.Single().Valor);
    }

    [Fact]
    public async Task RegistrarPagamento_AcimaDoSaldo_FalhaSemGravar()
    {
        var cliente = Cliente.Criar("Dona Maria");
        cliente.AdicionarFiado(Guid.NewGuid(), 50m);
        _clientes.Itens.Add(cliente);

        var handler = new RegistrarPagamentoFiadoHandler(_clientes, _uow);
        var result = await handler.Handle(new RegistrarPagamentoFiadoCommand(cliente.Id, 51m), default);

        Assert.True(result.IsFailure);
        Assert.Equal(50m, cliente.SaldoFiado);
        Assert.Equal(0, _uow.Commits);
    }

    [Fact]
    public async Task RegistrarPagamento_ClienteInexistente_Falha()
    {
        var handler = new RegistrarPagamentoFiadoHandler(_clientes, _uow);
        var result = await handler.Handle(new RegistrarPagamentoFiadoCommand(Guid.NewGuid(), 10m), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Cliente não encontrado.", result.Error);
    }
}
