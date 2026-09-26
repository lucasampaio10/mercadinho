using Microsoft.EntityFrameworkCore;
using PDV.Domain.Entities;
using PDV.Domain.Exceptions;
using PDV.Domain.Interfaces;

namespace PDV.Infrastructure.Persistence.Repositories;

// ── Base Repository ───────────────────────────────────────────────────────────

public abstract class RepositoryBase<T>(PdvDbContext context) : IRepository<T>
    where T : Entity
{
    protected readonly PdvDbContext _context = context;

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Set<T>().AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await _context.Set<T>().AddAsync(entity, ct);

    /// <summary>
    /// Só marca a entidade quando ela não está sendo rastreada. Chamar Update() numa
    /// entidade já rastreada marca o grafo inteiro como Modified — um pagamento de fiado
    /// emitiria UPDATE em todos os ItemFiado do cliente, não só nos que mudaram.
    /// Entidade rastreada não precisa de nada: o change tracker detecta as alterações.
    /// </summary>
    public void Update(T entity)
    {
        if (_context.Entry(entity).State == EntityState.Detached)
            _context.Set<T>().Update(entity);
    }

    public void Delete(T entity) =>
        _context.Set<T>().Remove(entity);
}

// ── Produto Repository ────────────────────────────────────────────────────────

public sealed class ProdutoRepository(PdvDbContext context)
    : RepositoryBase<Produto>(context), IProdutoRepository
{
    /// <summary>
    /// ILIKE em vez de <c>LOWER(Nome) LIKE</c>: a chamada a LOWER() na coluna torna o
    /// predicado não-sargável e descarta de saída o índice IX_produtos_Nome. Com ILIKE
    /// o prefixo da busca ainda pode ser aproveitado pelo planner, e o `%` à esquerda
    /// deixa de ser a única razão para o seq scan.
    /// </summary>
    public async Task<IEnumerable<Produto>> BuscarPorNomeAsync(string nome, CancellationToken ct = default)
    {
        var padrao = $"%{EscaparLike(nome)}%";
        return await _context.Produtos
            .AsNoTracking()
            .Where(p => p.Ativo && EF.Functions.ILike(p.Nome, padrao))
            .OrderBy(p => p.Nome)
            .ToListAsync(ct);
    }

    /// <summary>Neutraliza os curingas do LIKE para que o termo digitado seja literal.</summary>
    internal static string EscaparLike(string termo) =>
        termo.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>Lida exata — é o que a leitura da câmera devolve, sem margem para "parecido".</summary>
    public async Task<Produto?> BuscarPorCodigoBarrasAsync(string codigoBarras, CancellationToken ct = default) =>
        await _context.Produtos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Ativo && p.CodigoBarras == codigoBarras, ct);

    public async Task<IEnumerable<Produto>> ListarAtivosAsync(CancellationToken ct = default) =>
        await _context.Produtos
            .AsNoTracking()
            .Where(p => p.Ativo)
            .OrderBy(p => p.Nome)
            .ToListAsync(ct);

    /// <summary>Rastreado de propósito: o chamador vai mutar o estoque destes produtos.</summary>
    public async Task<IReadOnlyList<Produto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var lista = ids.Distinct().ToList();
        return await _context.Produtos
            .Where(p => lista.Contains(p.Id))
            .ToListAsync(ct);
    }

    public async Task RecarregarAsync(IEnumerable<Produto> produtos, CancellationToken ct = default)
    {
        foreach (var produto in produtos)
            await _context.Entry(produto).ReloadAsync(ct);
    }
}

// ── Cliente Repository ────────────────────────────────────────────────────────

public sealed class ClienteRepository(PdvDbContext context)
    : RepositoryBase<Cliente>(context), IClienteRepository
{
    /// <summary>
    /// Traz o agregado completo. O <c>HistoricoFiado</c> é obrigatório: o
    /// <see cref="Cliente.SaldoFiado"/> é recalculado a partir dele a cada mutação,
    /// então carregar o cliente sem os itens zeraria o saldo ao gravar.
    /// </summary>
    public override async Task<Cliente?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Clientes
            .Include(c => c.HistoricoFiado)
            .Include(c => c.Pagamentos)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IEnumerable<Cliente>> ListarAtivosAsync(CancellationToken ct = default) =>
        await _context.Clientes
            .AsNoTracking()
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .ToListAsync(ct);

    public async Task<IEnumerable<Cliente>> ListarComSaldoFiadoAsync(CancellationToken ct = default) =>
        await _context.Clientes
            .AsNoTracking()
            .Where(c => c.Ativo && c.SaldoFiado > 0)
            .OrderByDescending(c => c.SaldoFiado)
            .ToListAsync(ct);

    public async Task<Cliente?> BuscarPorNomeAsync(string nome, CancellationToken ct = default)
    {
        var padrao = $"%{ProdutoRepository.EscaparLike(nome)}%";
        return await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => EF.Functions.ILike(c.Nome, padrao), ct);
    }

    public async Task RegistrarNovoItemFiadoAsync(ItemFiado item, CancellationToken ct = default) =>
        await _context.ItensFiado.AddAsync(item, ct);

    public async Task RegistrarNovoPagamentoAsync(Pagamento pagamento, CancellationToken ct = default) =>
        await _context.Pagamentos.AddAsync(pagamento, ct);
}

// ── Venda Repository ──────────────────────────────────────────────────────────

public sealed class VendaRepository(PdvDbContext context)
    : RepositoryBase<Venda>(context), IVendaRepository
{
    public override async Task<Venda?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Vendas
            .Include(v => v.Itens)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<IEnumerable<Venda>> ListarPorPeriodoAsync(DateTime inicioUtc, DateTime fimUtc, CancellationToken ct = default) =>
        await _context.Vendas
            .AsNoTracking()
            .Include(v => v.Itens)
            .Where(v => v.CreatedAt >= inicioUtc && v.CreatedAt < fimUtc && v.Status == StatusVenda.Finalizada)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<FormaPagamento, decimal>> TotalPorFormaNoPeriodoAsync(
        DateTime inicioUtc, DateTime fimUtc, CancellationToken ct = default)
    {
        var totais = await _context.Vendas
            .AsNoTracking()
            .Where(v => v.Status == StatusVenda.Finalizada
                     && v.CreatedAt >= inicioUtc
                     && v.CreatedAt < fimUtc
                     && v.FormaPagamento != null)
            .GroupBy(v => v.FormaPagamento!.Value)
            .Select(g => new
            {
                Forma = g.Key,
                Total = g.SelectMany(v => v.Itens).Sum(i => i.PrecoUnitario * i.Quantidade)
            })
            .ToListAsync(ct);

        return totais.ToDictionary(t => t.Forma, t => t.Total);
    }

    public async Task<int> ContarNoPeriodoAsync(DateTime inicioUtc, DateTime fimUtc, CancellationToken ct = default) =>
        await _context.Vendas
            .AsNoTracking()
            .CountAsync(v => v.Status == StatusVenda.Finalizada
                          && v.CreatedAt >= inicioUtc
                          && v.CreatedAt < fimUtc, ct);

    public async Task RegistrarNovoItemAsync(ItemVenda item, CancellationToken ct = default) =>
        await _context.ItensVenda.AddAsync(item, ct);
}

// ── Unit of Work ──────────────────────────────────────────────────────────────

public sealed class UnitOfWork(PdvDbContext context) : IUnitOfWork
{
    /// <summary>
    /// Um único SaveChanges já roda em transação — tudo que estiver pendente
    /// (venda, itens, estoque, fiado) é gravado atomicamente ou nada é.
    /// O conflito otimista do EF é traduzido para <see cref="ConcorrenciaException"/>
    /// para que a Application não precise conhecer o ORM.
    /// </summary>
    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        try
        {
            return await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcorrenciaException(
                "Os registros foram alterados por outra operação durante a gravação.", ex);
        }
    }
}
