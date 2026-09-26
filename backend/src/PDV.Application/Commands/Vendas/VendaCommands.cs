using PDV.Application.Common;
using PDV.Application.DTOs;
using PDV.Domain.Entities;
using PDV.Domain.Exceptions;
using PDV.Domain.Interfaces;

namespace PDV.Application.Commands.Vendas;

// ── Iniciar Venda ─────────────────────────────────────────────────────────────

public record IniciarVendaCommand;

public sealed class IniciarVendaHandler(IVendaRepository repo, IUnitOfWork uow)
{
    /// <summary>Abre uma nova venda no balcão.</summary>
    public async Task<Result<Guid>> Handle(IniciarVendaCommand _, CancellationToken ct)
    {
        var venda = Venda.Iniciar();
        await repo.AddAsync(venda, ct);
        await uow.CommitAsync(ct);
        return Result.Success(venda.Id);
    }
}

// ── Adicionar Item ────────────────────────────────────────────────────────────

public record AdicionarItemCommand(Guid VendaId, Guid ProdutoId, int Quantidade);

/// <summary>Limites aceitos pela API para quantidade de item — espelham o teto do domínio.</summary>
public static class LimitesVenda
{
    public const int QuantidadeMinima = 1;
    public const int QuantidadeMaxima = ItemVenda.QuantidadeMaxima;
}

public sealed class AdicionarItemHandler(
    IVendaRepository vendaRepo,
    IProdutoRepository produtoRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Adiciona um produto ao carrinho da venda.
    /// O estoque é apenas <em>conferido</em> aqui — o débito acontece na finalização
    /// (<see cref="FinalizarVendaHandler"/>), para que um carrinho abandonado não
    /// retenha mercadoria. A conferência é só um aviso antecipado ao operador:
    /// a garantia real é o débito transacional no fechamento.
    /// Retorna a venda inteira para que o cliente sincronize o carrinho com o servidor
    /// em vez de manter uma cópia local que pode divergir.
    /// </summary>
    public async Task<Result<VendaResponse>> Handle(AdicionarItemCommand command, CancellationToken ct)
    {
        var venda = await vendaRepo.GetByIdAsync(command.VendaId, ct);
        if (venda is null) return Result.Failure<VendaResponse>("Venda não encontrada.");

        var produto = await produtoRepo.GetByIdAsync(command.ProdutoId, ct);
        if (produto is null) return Result.Failure<VendaResponse>("Produto não encontrado.");

        try
        {
            // Confere contra o total que o carrinho passará a ter, não só contra o incremento.
            var jaNoCarrinho = venda.Itens
                .FirstOrDefault(i => i.ProdutoId == command.ProdutoId)?.Quantidade ?? 0;
            produto.GarantirEstoqueDisponivel(jaNoCarrinho + command.Quantidade);

            var novoItem = venda.AdicionarItem(produto, command.Quantidade);

            // novoItem != null significa que o item foi criado agora (não existia no carrinho).
            // É necessário registrá-lo explicitamente no contexto para que o EF Core
            // gere um INSERT em vez de tentar um UPDATE em uma linha inexistente.
            if (novoItem is not null)
                await vendaRepo.RegistrarNovoItemAsync(novoItem, ct);

            await uow.CommitAsync(ct);
            return Result.Success(venda.ToResponse());
        }
        catch (DomainException ex)
        {
            return Result.Failure<VendaResponse>(ex.Message);
        }
    }
}

// ── Remover Item ──────────────────────────────────────────────────────────────

public record RemoverItemCommand(Guid VendaId, Guid ProdutoId);

public sealed class RemoverItemHandler(IVendaRepository vendaRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Remove um produto do carrinho.
    /// Não mexe em estoque: nada foi debitado ao adicionar.
    /// Retorna a venda atualizada — mesma razão do <see cref="AdicionarItemHandler"/>.
    /// </summary>
    public async Task<Result<VendaResponse>> Handle(RemoverItemCommand command, CancellationToken ct)
    {
        var venda = await vendaRepo.GetByIdAsync(command.VendaId, ct);
        if (venda is null) return Result.Failure<VendaResponse>("Venda não encontrada.");

        try
        {
            venda.RemoverItem(command.ProdutoId);
            // A venda já está rastreada; o item removido da coleção vira órfão e o EF
            // emite o DELETE por conta da cascata configurada.
            await uow.CommitAsync(ct);
            return Result.Success(venda.ToResponse());
        }
        catch (DomainException ex)
        {
            return Result.Failure<VendaResponse>(ex.Message);
        }
    }
}

// ── Alterar Quantidade do Item ────────────────────────────────────────────────

public record AlterarQuantidadeItemCommand(Guid VendaId, Guid ProdutoId, int Quantidade);

public sealed class AlterarQuantidadeItemHandler(
    IVendaRepository vendaRepo,
    IProdutoRepository produtoRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Define a quantidade exata de um item do carrinho.
    /// Sem isto, corrigir "digitei 10, era 1" obriga a remover o item e refazê-lo.
    /// Como o débito de estoque só ocorre na finalização, aqui apenas se confere
    /// a disponibilidade — mesma política do <see cref="AdicionarItemHandler"/>.
    /// </summary>
    public async Task<Result<VendaResponse>> Handle(AlterarQuantidadeItemCommand command, CancellationToken ct)
    {
        var venda = await vendaRepo.GetByIdAsync(command.VendaId, ct);
        if (venda is null) return Result.Failure<VendaResponse>("Venda não encontrada.");

        var produto = await produtoRepo.GetByIdAsync(command.ProdutoId, ct);
        if (produto is null) return Result.Failure<VendaResponse>("Produto não encontrado.");

        try
        {
            produto.GarantirEstoqueDisponivel(command.Quantidade);
            venda.AlterarQuantidadeItem(command.ProdutoId, command.Quantidade);

            await uow.CommitAsync(ct);
            return Result.Success(venda.ToResponse());
        }
        catch (DomainException ex)
        {
            return Result.Failure<VendaResponse>(ex.Message);
        }
    }
}

// ── Cancelar Venda ────────────────────────────────────────────────────────────

public record CancelarVendaCommand(Guid VendaId);

public sealed class CancelarVendaHandler(IVendaRepository vendaRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Descarta uma venda aberta. Como o estoque só é debitado na finalização,
    /// cancelar não devolve mercadoria — apenas fecha a venda órfã.
    /// </summary>
    public async Task<Result> Handle(CancelarVendaCommand command, CancellationToken ct)
    {
        var venda = await vendaRepo.GetByIdAsync(command.VendaId, ct);
        if (venda is null) return Result.Failure("Venda não encontrada.");

        try
        {
            venda.Cancelar();
            await uow.CommitAsync(ct);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}

// ── Finalizar Venda ───────────────────────────────────────────────────────────

public record FinalizarVendaCommand(
    Guid VendaId,
    string FormaPagamento, // "Dinheiro" | "Cartao" | "Fiado"
    Guid? ClienteId,
    string? Observacao = null
);

public sealed class FinalizarVendaHandler(
    IVendaRepository vendaRepo,
    IProdutoRepository produtoRepo,
    IClienteRepository clienteRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Quantas vezes refazer o débito quando outro caixa alterou o mesmo produto
    /// entre a leitura e a gravação.
    /// </summary>
    private const int MaxTentativas = 3;

    /// <summary>
    /// Finaliza a venda: debita o estoque de todos os itens e, se for fiado,
    /// lança no histórico do cliente — tudo numa única transação.
    /// O débito é protegido por token de concorrência: se outro caixa levou o estoque
    /// nesse intervalo, os produtos são relidos e o débito refeito. Se o estoque não
    /// cobrir mais a venda, ela falha sem gravar nada.
    /// </summary>
    public async Task<Result<VendaResponse>> Handle(FinalizarVendaCommand command, CancellationToken ct)
    {
        var venda = await vendaRepo.GetByIdAsync(command.VendaId, ct);
        if (venda is null) return Result.Failure<VendaResponse>("Venda não encontrada.");

        // TryParse sozinho aceita qualquer número: "99" viraria FormaPagamento 99 e
        // seria gravado como a string "99" no banco. IsDefined recusa o que não é do enum.
        if (!Enum.TryParse<FormaPagamento>(command.FormaPagamento, ignoreCase: true, out var forma)
            || !Enum.IsDefined(forma))
            return Result.Failure<VendaResponse>("Forma de pagamento inválida.");

        var produtos = await produtoRepo.GetByIdsAsync(
            venda.Itens.Select(i => i.ProdutoId), ct);

        var faltando = venda.Itens.Select(i => i.ProdutoId)
            .Except(produtos.Select(p => p.Id))
            .Any();
        if (faltando) return Result.Failure<VendaResponse>("Algum produto da venda não existe mais.");

        Cliente? cliente = null;
        try
        {
            venda.Finalizar(forma, command.ClienteId, command.Observacao);

            // Se fiado, lança no cliente
            if (forma == FormaPagamento.Fiado && command.ClienteId.HasValue)
            {
                cliente = await clienteRepo.GetByIdAsync(command.ClienteId.Value, ct);
                if (cliente is null) return Result.Failure<VendaResponse>("Cliente não encontrado.");

                // AdicionarFiado retorna o novo ItemFiado — precisa ser registrado
                // explicitamente para o EF Core gerar INSERT (não UPDATE).
                var novoItemFiado = cliente.AdicionarFiado(venda.Id, venda.Total);
                await clienteRepo.RegistrarNovoItemFiadoAsync(novoItemFiado, ct);
                // cliente (SaldoFiado, UpdatedAt) já está rastreado — EF detecta automaticamente.
            }

            for (var tentativa = 1; ; tentativa++)
            {
                try
                {
                    DebitarEstoque(venda, produtos);
                    // venda e cliente já estão rastreados — um único SaveChanges cobre tudo.
                    await uow.CommitAsync(ct);
                    break;
                }
                catch (ConcorrenciaException) when (tentativa < MaxTentativas)
                {
                    // Relê o estoque real e refaz o débito do zero sobre os valores novos.
                    await produtoRepo.RecarregarAsync(produtos, ct);
                }
            }

            return Result.Success(venda.ToResponse(cliente?.Nome));
        }
        catch (DomainException ex)
        {
            return Result.Failure<VendaResponse>(ex.Message);
        }
        catch (ConcorrenciaException)
        {
            return Result.Failure<VendaResponse>(
                "O estoque está sendo alterado por outra operação. Tente finalizar novamente.");
        }
    }

    private static void DebitarEstoque(Venda venda, IReadOnlyList<Produto> produtos)
    {
        foreach (var item in venda.Itens)
            produtos.First(p => p.Id == item.ProdutoId).DebitarEstoque(item.Quantidade);
    }
}

// ── Mapper ────────────────────────────────────────────────────────────────────

internal static class VendaMapper
{
    /// <summary>
    /// `clienteNome` é opcional porque nem todo chamador tem o cliente carregado — quem
    /// tiver (finalização no fiado, listagem de vendas) passa o nome; os demais deixam
    /// nulo e o front simplesmente não mostra o cliente.
    /// </summary>
    internal static VendaResponse ToResponse(this Venda v, string? clienteNome = null) => new(
        v.Id,
        v.Status.ToString(),
        v.FormaPagamento?.ToString(),
        v.ClienteId,
        clienteNome,
        v.Total,
        v.Itens.Select(i => new ItemVendaResponse(i.ProdutoId, i.NomeProduto, i.PrecoUnitario, i.Quantidade, i.Subtotal)),
        v.Observacao,
        v.CreatedAt
    );
}
