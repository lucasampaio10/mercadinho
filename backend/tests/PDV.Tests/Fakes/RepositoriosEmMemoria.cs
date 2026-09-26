using PDV.Domain.Entities;
using PDV.Domain.Interfaces;

namespace PDV.Tests.Fakes;

/// <summary>
/// Repositórios em memória para exercitar os handlers sem banco.
/// Deliberadamente burros: guardam as entidades e devolvem a mesma instância, que é
/// o comportamento do EF com change tracking. O que se testa aqui é a orquestração
/// do handler e a regra de domínio — não o mapeamento relacional.
/// </summary>
public sealed class ProdutoRepositoryFake : IProdutoRepository
{
    public List<Produto> Itens { get; } = [];

    public Task<Produto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(p => p.Id == id));

    public Task<IEnumerable<Produto>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IEnumerable<Produto>>(Itens);

    public Task AddAsync(Produto entity, CancellationToken ct = default)
    {
        Itens.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Produto entity) { }
    public void Delete(Produto entity) => Itens.Remove(entity);

    public Task<IEnumerable<Produto>> BuscarPorNomeAsync(string nome, CancellationToken ct = default) =>
        Task.FromResult(Itens.Where(p => p.Ativo && p.Nome.Contains(nome, StringComparison.OrdinalIgnoreCase)));

    public Task<Produto?> BuscarPorCodigoBarrasAsync(string codigoBarras, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(p => p.Ativo && p.CodigoBarras == codigoBarras));

    public Task<IEnumerable<Produto>> ListarAtivosAsync(CancellationToken ct = default) =>
        Task.FromResult(Itens.Where(p => p.Ativo));

    public Task<IReadOnlyList<Produto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var set = ids.ToHashSet();
        return Task.FromResult<IReadOnlyList<Produto>>(Itens.Where(p => set.Contains(p.Id)).ToList());
    }

    public Task RecarregarAsync(IEnumerable<Produto> produtos, CancellationToken ct = default) =>
        Task.CompletedTask;
}

public sealed class VendaRepositoryFake : IVendaRepository
{
    public List<Venda> Itens { get; } = [];
    public List<ItemVenda> ItensRegistrados { get; } = [];

    public Task<Venda?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(v => v.Id == id));

    public Task<IEnumerable<Venda>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IEnumerable<Venda>>(Itens);

    public Task AddAsync(Venda entity, CancellationToken ct = default)
    {
        Itens.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Venda entity) { }
    public void Delete(Venda entity) => Itens.Remove(entity);

    public Task<IEnumerable<Venda>> ListarPorPeriodoAsync(DateTime inicioUtc, DateTime fimUtc, CancellationToken ct = default) =>
        Task.FromResult(Itens.Where(v =>
            v.Status == StatusVenda.Finalizada && v.CreatedAt >= inicioUtc && v.CreatedAt < fimUtc));

    public Task<IReadOnlyDictionary<FormaPagamento, decimal>> TotalPorFormaNoPeriodoAsync(
        DateTime inicioUtc, DateTime fimUtc, CancellationToken ct = default)
    {
        var totais = Itens
            .Where(v => v.Status == StatusVenda.Finalizada
                     && v.FormaPagamento is not null
                     && v.CreatedAt >= inicioUtc && v.CreatedAt < fimUtc)
            .GroupBy(v => v.FormaPagamento!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(v => v.Total));

        return Task.FromResult<IReadOnlyDictionary<FormaPagamento, decimal>>(totais);
    }

    public Task<int> ContarNoPeriodoAsync(DateTime inicioUtc, DateTime fimUtc, CancellationToken ct = default) =>
        Task.FromResult(Itens.Count(v =>
            v.Status == StatusVenda.Finalizada && v.CreatedAt >= inicioUtc && v.CreatedAt < fimUtc));

    public Task RegistrarNovoItemAsync(ItemVenda item, CancellationToken ct = default)
    {
        ItensRegistrados.Add(item);
        return Task.CompletedTask;
    }
}

public sealed class ClienteRepositoryFake : IClienteRepository
{
    public List<Cliente> Itens { get; } = [];
    public List<ItemFiado> FiadosRegistrados { get; } = [];
    public List<Pagamento> PagamentosRegistrados { get; } = [];

    public Task<Cliente?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(c => c.Id == id));

    public Task<IEnumerable<Cliente>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IEnumerable<Cliente>>(Itens);

    public Task AddAsync(Cliente entity, CancellationToken ct = default)
    {
        Itens.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Cliente entity) { }
    public void Delete(Cliente entity) => Itens.Remove(entity);

    public Task<IEnumerable<Cliente>> ListarAtivosAsync(CancellationToken ct = default) =>
        Task.FromResult(Itens.Where(c => c.Ativo));

    public Task<IEnumerable<Cliente>> ListarComSaldoFiadoAsync(CancellationToken ct = default) =>
        Task.FromResult(Itens.Where(c => c.Ativo && c.SaldoFiado > 0));

    public Task<Cliente?> BuscarPorNomeAsync(string nome, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(c => c.Nome.Contains(nome, StringComparison.OrdinalIgnoreCase)));

    public Task RegistrarNovoItemFiadoAsync(ItemFiado item, CancellationToken ct = default)
    {
        FiadosRegistrados.Add(item);
        return Task.CompletedTask;
    }

    public Task RegistrarNovoPagamentoAsync(Pagamento pagamento, CancellationToken ct = default)
    {
        PagamentosRegistrados.Add(pagamento);
        return Task.CompletedTask;
    }
}

/// <summary>Conta os commits — serve para afirmar que um caminho de falha não gravou nada.</summary>
public sealed class UnitOfWorkFake : IUnitOfWork
{
    public int Commits { get; private set; }

    public Task<int> CommitAsync(CancellationToken ct = default)
    {
        Commits++;
        return Task.FromResult(1);
    }
}
