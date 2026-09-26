using Microsoft.EntityFrameworkCore;
using PDV.Application.Commands.Clientes;
using PDV.Application.Commands.Produtos;
using PDV.Application.Commands.Vendas;
using PDV.Application.Queries;
using PDV.Domain.Interfaces;
using PDV.Infrastructure.Persistence;
using PDV.Infrastructure.Persistence.Repositories;

namespace PDV.API.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<PdvDbContext>(options =>
            options.UseNpgsql(
                configuration["POSTGRES_CONNECTION_STRING"]
                ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING não configurada.")));

        services.AddScoped<IProdutoRepository, ProdutoRepository>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IVendaRepository, VendaRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Commands — Produtos
        services.AddScoped<CriarProdutoHandler>();
        services.AddScoped<AtualizarProdutoHandler>();
        services.AddScoped<InativarProdutoHandler>();

        // Commands — Vendas
        services.AddScoped<IniciarVendaHandler>();
        services.AddScoped<AdicionarItemHandler>();
        services.AddScoped<AlterarQuantidadeItemHandler>();
        services.AddScoped<RemoverItemHandler>();
        services.AddScoped<CancelarVendaHandler>();
        services.AddScoped<FinalizarVendaHandler>();

        // Commands — Clientes
        services.AddScoped<CriarClienteHandler>();
        services.AddScoped<RegistrarPagamentoFiadoHandler>();

        // Queries
        services.AddScoped<ListarProdutosQuery>();
        services.AddScoped<BuscarProdutoQuery>();
        services.AddScoped<BuscarProdutoPorCodigoBarrasQuery>();
        services.AddScoped<ListarClientesQuery>();
        services.AddScoped<ListarClientesFiadoQuery>();
        services.AddScoped<HistoricoFiadoQuery>();
        services.AddScoped<ObterVendaQuery>();
        services.AddScoped<ListarVendasDoDiaQuery>();
        services.AddScoped<DashboardQuery>();

        return services;
    }

    /// <summary>
    /// Identificador arbitrário e fixo do lock de migrations deste banco.
    /// Só precisa ser o mesmo em todos os processos do PDV.
    /// </summary>
    private const long MigrationLockId = 8_314_072_026;

    /// <summary>
    /// Aplica as migrations sob um advisory lock do PostgreSQL.
    /// Sem ele, dois processos subindo juntos (um restart durante o outro, API e
    /// worker, dois contêineres) disputam a tabela __EFMigrationsHistory e um deles
    /// morre com erro de objeto duplicado. O lock é de sessão: só é liberado ao final,
    /// e a conexão é mantida aberta de propósito durante toda a migração.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PdvDbContext>();

        await db.Database.OpenConnectionAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock({0})", MigrationLockId);
            try
            {
                await db.Database.MigrateAsync();
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0})", MigrationLockId);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
