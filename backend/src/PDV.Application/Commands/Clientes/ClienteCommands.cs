using System.ComponentModel.DataAnnotations;
using PDV.Application.Common;
using PDV.Application.DTOs;
using PDV.Domain.Entities;
using PDV.Domain.Interfaces;

namespace PDV.Application.Commands.Clientes;

/// <summary>Limites aceitos pela API — espelham as colunas e as regras de <see cref="Cliente"/>.</summary>
public static class LimitesCliente
{
    public const int TamanhoNome = 200;
    public const int TamanhoTelefone = 20;
    public const double ValorMinimo = 0.01;
    public const double ValorMaximo = 1_000_000;
}

// ── Criar Cliente ─────────────────────────────────────────────────────────────

public record CriarClienteCommand(
    [Required(ErrorMessage = "Nome do cliente é obrigatório.")]
    [StringLength(LimitesCliente.TamanhoNome, MinimumLength = 1,
        ErrorMessage = "Nome deve ter entre 1 e {1} caracteres.")]
    string Nome,

    [StringLength(LimitesCliente.TamanhoTelefone,
        ErrorMessage = "Telefone deve ter no máximo {1} caracteres.")]
    string? Telefone
);

public sealed class CriarClienteHandler(IClienteRepository repo, IUnitOfWork uow)
{
    /// <summary>Cadastra um novo cliente no sistema de fiado.</summary>
    public async Task<Result<ClienteResponse>> Handle(CriarClienteCommand command, CancellationToken ct)
    {
        try
        {
            var cliente = Cliente.Criar(command.Nome, command.Telefone);
            await repo.AddAsync(cliente, ct);
            await uow.CommitAsync(ct);
            return Result.Success(cliente.ToResponse());
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            return Result.Failure<ClienteResponse>(ex.Message);
        }
    }
}

// ── Registrar Pagamento de Fiado ──────────────────────────────────────────────

public record RegistrarPagamentoFiadoCommand(Guid ClienteId, decimal Valor);

public sealed class RegistrarPagamentoFiadoHandler(IClienteRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Registra pagamento (parcial ou total) do fiado de um cliente.
    /// Marca os itens mais antigos como pagos em ordem FIFO.
    /// </summary>
    public async Task<Result> Handle(RegistrarPagamentoFiadoCommand command, CancellationToken ct)
    {
        var cliente = await repo.GetByIdAsync(command.ClienteId, ct);
        if (cliente is null) return Result.Failure("Cliente não encontrado.");

        try
        {
            // O FIFO (incluindo abatimento parcial) é responsabilidade do agregado Cliente.
            var pagamento = cliente.RegistrarPagamento(command.Valor);

            // Novo Pagamento precisa ser registrado explicitamente para o EF gerar INSERT.
            await repo.RegistrarNovoPagamentoAsync(pagamento, ct);

            // Nada de repo.Update(cliente): o agregado já está rastreado, e Update()
            // percorreria o grafo marcando o Pagamento recém-adicionado como Modified —
            // o EF emitiria UPDATE numa linha que ainda não existe.
            await uow.CommitAsync(ct);
            return Result.Success();
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}

// ── Mapper ────────────────────────────────────────────────────────────────────

internal static class ClienteMapper
{
    internal static ClienteResponse ToResponse(this Cliente c) =>
        new(c.Id, c.Nome, c.Telefone, c.SaldoFiado, c.Ativo);
}
