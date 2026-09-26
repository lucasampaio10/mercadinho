using PDV.Application.Commands.Vendas;
using PDV.Application.Common;
using PDV.Application.DTOs;
using PDV.Domain.Entities;
using PDV.Domain.Interfaces;

namespace PDV.Application.Queries;

// ── Produtos ──────────────────────────────────────────────────────────────────

public sealed class ListarProdutosQuery(IProdutoRepository repo)
{
    /// <summary>Lista todos os produtos ativos.</summary>
    public async Task<IEnumerable<ProdutoResponse>> Handle(CancellationToken ct) =>
        (await repo.ListarAtivosAsync(ct))
            .Select(p => new ProdutoResponse(p.Id, p.Nome, p.Categoria, p.CodigoBarras, p.Preco, p.Estoque, p.Ativo, p.CreatedAt));
}

public sealed class BuscarProdutoQuery(IProdutoRepository repo)
{
    /// <summary>Busca produtos pelo nome (busca parcial).</summary>
    public async Task<IEnumerable<ProdutoResponse>> Handle(string nome, CancellationToken ct) =>
        (await repo.BuscarPorNomeAsync(nome, ct))
            .Select(p => new ProdutoResponse(p.Id, p.Nome, p.Categoria, p.CodigoBarras, p.Preco, p.Estoque, p.Ativo, p.CreatedAt));
}

public sealed class BuscarProdutoPorCodigoBarrasQuery(IProdutoRepository repo)
{
    /// <summary>Busca o produto pelo código de barras exato (leitura da câmera).</summary>
    public async Task<ProdutoResponse?> Handle(string codigoBarras, CancellationToken ct)
    {
        var produto = await repo.BuscarPorCodigoBarrasAsync(codigoBarras, ct);
        return produto is null
            ? null
            : new ProdutoResponse(produto.Id, produto.Nome, produto.Categoria, produto.CodigoBarras, produto.Preco, produto.Estoque, produto.Ativo, produto.CreatedAt);
    }
}

// ── Clientes ──────────────────────────────────────────────────────────────────

public sealed class ListarClientesQuery(IClienteRepository repo)
{
    /// <summary>Lista todos os clientes ativos (para seleção no fiado).</summary>
    public async Task<IEnumerable<ClienteResponse>> Handle(CancellationToken ct) =>
        (await repo.ListarAtivosAsync(ct))
            .Select(c => new ClienteResponse(c.Id, c.Nome, c.Telefone, c.SaldoFiado, c.Ativo));
}

public sealed class ListarClientesFiadoQuery(IClienteRepository repo)
{
    /// <summary>Lista apenas clientes com saldo de fiado pendente.</summary>
    public async Task<IEnumerable<ClienteResponse>> Handle(CancellationToken ct) =>
        (await repo.ListarComSaldoFiadoAsync(ct))
            .Select(c => new ClienteResponse(c.Id, c.Nome, c.Telefone, c.SaldoFiado, c.Ativo));
}

public sealed class HistoricoFiadoQuery(IClienteRepository repo)
{
    /// <summary>
    /// Extrato de fiado do cliente: compras e pagamentos.
    /// Sem os pagamentos, o cliente vê o que deve mas não o que já quitou.
    /// </summary>
    public async Task<HistoricoFiadoResponse?> Handle(Guid clienteId, CancellationToken ct)
    {
        var cliente = await repo.GetByIdAsync(clienteId, ct);
        if (cliente is null) return null;

        var historico = cliente.HistoricoFiado
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ItemFiadoResponse(
                i.Id, i.VendaId, i.Valor, i.ValorPago, i.Saldo, i.Pago, i.PagoEm, i.CreatedAt));

        var pagamentos = cliente.Pagamentos
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PagamentoResponse(p.Id, p.Valor, p.CreatedAt));

        return new HistoricoFiadoResponse(
            new ClienteResponse(cliente.Id, cliente.Nome, cliente.Telefone, cliente.SaldoFiado, cliente.Ativo),
            historico,
            pagamentos);
    }
}

// ── Vendas ────────────────────────────────────────────────────────────────────

public sealed class ObterVendaQuery(IVendaRepository repo, IClienteRepository clienteRepo)
{
    /// <summary>
    /// Retorna uma venda com seus itens. É a fonte de verdade do carrinho:
    /// permite ao PDV recuperar a venda aberta após um refresh da página.
    /// </summary>
    public async Task<VendaResponse?> Handle(Guid vendaId, CancellationToken ct)
    {
        var venda = await repo.GetByIdAsync(vendaId, ct);
        if (venda is null) return null;

        var clienteNome = venda.ClienteId.HasValue
            ? (await clienteRepo.GetByIdAsync(venda.ClienteId.Value, ct))?.Nome
            : null;
        return venda.ToResponse(clienteNome);
    }
}

public sealed class ListarVendasDoDiaQuery(IVendaRepository repo, IClienteRepository clienteRepo)
{
    /// <summary>Vendas finalizadas hoje (fuso local), mais recentes primeiro — para a tela "Vendas".</summary>
    public async Task<IEnumerable<VendaResponse>> Handle(CancellationToken ct)
    {
        var (inicioUtc, fimUtc) = FusoLocal.IntervaloDeHojeUtc();
        var vendas = (await repo.ListarPorPeriodoAsync(inicioUtc, fimUtc, ct)).ToList();

        // Poucas vendas no fiado por dia num mercadinho — não compensa criar um
        // GetByIdsAsync em IClienteRepository só para evitar esse N+1.
        var nomesPorCliente = new Dictionary<Guid, string>();
        foreach (var clienteId in vendas.Where(v => v.ClienteId.HasValue).Select(v => v.ClienteId!.Value).Distinct())
        {
            var cliente = await clienteRepo.GetByIdAsync(clienteId, ct);
            if (cliente is not null) nomesPorCliente[clienteId] = cliente.Nome;
        }

        return vendas.Select(v => v.ToResponse(v.ClienteId.HasValue ? nomesPorCliente.GetValueOrDefault(v.ClienteId.Value) : null));
    }
}

// ── Dashboard ─────────────────────────────────────────────────────────────────

public sealed class DashboardQuery(IVendaRepository vendaRepo, IClienteRepository clienteRepo)
{
    /// <summary>Retorna resumo do dia para a tela inicial.</summary>
    public async Task<DashboardResponse> Handle(CancellationToken ct)
    {
        // "Hoje" é o dia civil do mercadinho, não o dia UTC.
        var (inicioUtc, fimUtc) = FusoLocal.IntervaloDeHojeUtc();

        var totaisPorForma = await vendaRepo.TotalPorFormaNoPeriodoAsync(inicioUtc, fimUtc, ct);
        var vendasHoje = await vendaRepo.ContarNoPeriodoAsync(inicioUtc, fimUtc, ct);
        var clientesComFiado = await clienteRepo.ListarComSaldoFiadoAsync(ct);
        var clientesList = clientesComFiado.ToList();

        var fiadoHoje = totaisPorForma.GetValueOrDefault(FormaPagamento.Fiado);
        var totalHoje = totaisPorForma.Values.Sum();

        return new DashboardResponse(
            TotalVendidoHoje: totalHoje,
            TotalRecebidoHoje: totalHoje - fiadoHoje,
            TotalFiadoHoje: fiadoHoje,
            TotalVendasHoje: vendasHoje,
            TotalFiadoPendente: clientesList.Sum(c => c.SaldoFiado),
            ClientesComFiado: clientesList.Count
        );
    }
}
