using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Aggregate root do contexto de Produtos.
/// </summary>
public sealed class Produto : Entity
{
    public string Nome { get; private set; } = string.Empty;
    public string? Categoria { get; private set; }
    public string? CodigoBarras { get; private set; }
    public decimal Preco { get; private set; }
    public int Estoque { get; private set; }
    public bool Ativo { get; private set; } = true;

    private Produto() { } // EF Core

    public static Produto Criar(string nome, decimal preco, int estoque, string? categoria = null, string? codigoBarras = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("Nome do produto é obrigatório.");
        // Maior que zero, não apenas não-negativo: o front já recusava R$ 0,00 e o
        // domínio aceitava — produto a preço zero passava pela API e quebrava o caixa.
        if (preco <= 0)
            throw new DomainException("Preço deve ser maior que zero.");
        if (estoque < 0)
            throw new DomainException("Estoque não pode ser negativo.");

        return new Produto
        {
            Nome = nome.Trim(),
            Preco = preco,
            Estoque = estoque,
            Categoria = categoria?.Trim(),
            CodigoBarras = NormalizarCodigoBarras(codigoBarras)
        };
    }

    public void Atualizar(string nome, decimal preco, int estoque, string? categoria, string? codigoBarras = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("Nome do produto é obrigatório.");
        // Maior que zero, não apenas não-negativo: o front já recusava R$ 0,00 e o
        // domínio aceitava — produto a preço zero passava pela API e quebrava o caixa.
        if (preco <= 0)
            throw new DomainException("Preço deve ser maior que zero.");
        if (estoque < 0)
            throw new DomainException("Estoque não pode ser negativo.");

        Nome = nome.Trim();
        Preco = preco;
        Estoque = estoque;
        Categoria = categoria?.Trim();
        CodigoBarras = NormalizarCodigoBarras(codigoBarras);
        Touch();
    }

    // Vazio vira null: uma string vazia não deve colidir com outro produto sem
    // código de barras no índice único de CodigoBarras.
    private static string? NormalizarCodigoBarras(string? codigoBarras)
    {
        var texto = codigoBarras?.Trim();
        return string.IsNullOrEmpty(texto) ? null : texto;
    }

    /// <summary>
    /// Verifica se há estoque para a quantidade sem alterar o produto.
    /// Usado ao montar o carrinho — o débito só acontece na finalização da venda.
    /// </summary>
    public void GarantirEstoqueDisponivel(int quantidade)
    {
        if (quantidade <= 0)
            throw new DomainException("Quantidade deve ser maior que zero.");
        if (Estoque < quantidade)
            throw new DomainException($"Estoque insuficiente de \"{Nome}\". Disponível: {Estoque}.");
    }

    public void DebitarEstoque(int quantidade)
    {
        GarantirEstoqueDisponivel(quantidade);

        Estoque -= quantidade;
        Touch();
    }

    /// <summary>Devolve quantidade ao estoque (estorno de venda cancelada).</summary>
    public void CreditarEstoque(int quantidade)
    {
        if (quantidade <= 0)
            throw new DomainException("Quantidade deve ser maior que zero.");

        Estoque += quantidade;
        Touch();
    }

    public void Inativar()
    {
        Ativo = false;
        Touch();
    }
}
