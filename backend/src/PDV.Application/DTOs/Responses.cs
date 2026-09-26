using PDV.Domain.Entities;

namespace PDV.Application.DTOs;

// ── Produtos ──────────────────────────────────────────────────────────────────

public record ProdutoResponse(
    Guid Id,
    string Nome,
    string? Categoria,
    string? CodigoBarras,
    decimal Preco,
    int Estoque,
    bool Ativo,
    DateTime CreatedAt
);

// ── Clientes ──────────────────────────────────────────────────────────────────

public record ClienteResponse(
    Guid Id,
    string Nome,
    string? Telefone,
    decimal SaldoFiado,
    bool Ativo
);

public record ItemFiadoResponse(
    Guid Id,
    Guid VendaId,
    decimal Valor,
    decimal ValorPago,
    decimal Saldo,
    bool Pago,
    DateTime? PagoEm,
    DateTime CreatedAt
);

public record PagamentoResponse(
    Guid Id,
    decimal Valor,
    DateTime CreatedAt
);

/// <summary>Extrato do fiado: as compras e os pagamentos do cliente.</summary>
public record HistoricoFiadoResponse(
    ClienteResponse Cliente,
    IEnumerable<ItemFiadoResponse> Historico,
    IEnumerable<PagamentoResponse> Pagamentos
);

// ── Vendas ────────────────────────────────────────────────────────────────────

public record ItemVendaResponse(
    Guid ProdutoId,
    string NomeProduto,
    decimal PrecoUnitario,
    int Quantidade,
    decimal Subtotal
);

public record VendaResponse(
    Guid Id,
    string Status,
    string? FormaPagamento,
    Guid? ClienteId,
    string? ClienteNome,
    decimal Total,
    IEnumerable<ItemVendaResponse> Itens,
    string? Observacao,
    DateTime CreatedAt
);

// ── Dashboard ─────────────────────────────────────────────────────────────────

/// <summary>
/// Resumo do dia. <see cref="TotalVendidoHoje"/> é o giro (tudo que saiu do balcão);
/// <see cref="TotalRecebidoHoje"/> é o que virou caixa de verdade e
/// <see cref="TotalFiadoHoje"/> o que saiu fiado. Sem essa separação, uma venda no
/// fiado aparece somada ao total vendido e ao fiado pendente ao mesmo tempo, e o
/// fechamento do dia não bate com o dinheiro na gaveta.
/// </summary>
public record DashboardResponse(
    decimal TotalVendidoHoje,
    decimal TotalRecebidoHoje,
    decimal TotalFiadoHoje,
    int TotalVendasHoje,
    decimal TotalFiadoPendente,
    int ClientesComFiado
);
