using PDV.Domain.Entities;

namespace PDV.Domain.Interfaces;

public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
}

public interface IProdutoRepository : IRepository<Produto>
{
    Task<IEnumerable<Produto>> BuscarPorNomeAsync(string nome, CancellationToken ct = default);
    Task<Produto?> BuscarPorCodigoBarrasAsync(string codigoBarras, CancellationToken ct = default);
    Task<IEnumerable<Produto>> ListarAtivosAsync(CancellationToken ct = default);

    /// <summary>Carrega vários produtos rastreados de uma vez — usado ao debitar o estoque da venda.</summary>
    Task<IReadOnlyList<Produto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Relê os produtos do banco, descartando as alterações pendentes em memória.
    /// Usado para refazer o débito de estoque após uma <see cref="Exceptions.ConcorrenciaException"/>.
    /// </summary>
    Task RecarregarAsync(IEnumerable<Produto> produtos, CancellationToken ct = default);
}

public interface IClienteRepository : IRepository<Cliente>
{
    Task<IEnumerable<Cliente>> ListarAtivosAsync(CancellationToken ct = default);
    Task<IEnumerable<Cliente>> ListarComSaldoFiadoAsync(CancellationToken ct = default);
    Task<Cliente?> BuscarPorNomeAsync(string nome, CancellationToken ct = default);
    Task RegistrarNovoItemFiadoAsync(ItemFiado item, CancellationToken ct = default);
    Task RegistrarNovoPagamentoAsync(Pagamento pagamento, CancellationToken ct = default);
}

public interface IVendaRepository : IRepository<Venda>
{
    /// <summary>
    /// Vendas finalizadas no intervalo UTC [inicioUtc, fimUtc). Quem chama define o
    /// recorte do dia — a conversão de fuso é política da camada de aplicação.
    /// </summary>
    Task<IEnumerable<Venda>> ListarPorPeriodoAsync(DateTime inicioUtc, DateTime fimUtc, CancellationToken ct = default);

    /// <summary>
    /// Soma das vendas finalizadas no intervalo UTC [inicioUtc, fimUtc), quebrada por
    /// forma de pagamento. O fechamento do caixa precisa separar o que entrou em
    /// dinheiro do que virou dívida no fiado — um total único confunde os dois.
    /// Formas sem venda no período não aparecem no dicionário.
    /// </summary>
    Task<IReadOnlyDictionary<FormaPagamento, decimal>> TotalPorFormaNoPeriodoAsync(
        DateTime inicioUtc, DateTime fimUtc, CancellationToken ct = default);

    /// <summary>
    /// Quantidade de vendas finalizadas no intervalo UTC [inicioUtc, fimUtc).
    /// COUNT no banco — não carrega as vendas nem os itens só para contar.
    /// </summary>
    Task<int> ContarNoPeriodoAsync(DateTime inicioUtc, DateTime fimUtc, CancellationToken ct = default);
    /// <summary>Registra um novo ItemVenda no contexto para que seja inserido no banco.</summary>
    Task RegistrarNovoItemAsync(ItemVenda item, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    /// <summary>
    /// Persiste tudo que está pendente numa única transação.
    /// Lança <see cref="Exceptions.ConcorrenciaException"/> se outro processo alterou
    /// os mesmos registros entre a leitura e a gravação.
    /// </summary>
    Task<int> CommitAsync(CancellationToken ct = default);
}
