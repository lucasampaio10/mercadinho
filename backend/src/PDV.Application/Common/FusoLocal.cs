namespace PDV.Application.Common;

/// <summary>
/// Política de datas do PDV.
///
/// Tudo é persistido em UTC (Entity.CreatedAt), mas "hoje" para o
/// mercadinho é o dia civil do fuso local — não o dia UTC. Sem isso, no Brasil
/// (UTC-3) toda venda a partir das 21h já cairia no dia seguinte e o
/// "Resumo de Hoje" zeraria antes do fechamento da noite.
/// </summary>
public static class FusoLocal
{
    private const string IanaId = "America/Sao_Paulo";
    private const string WindowsId = "E. South America Standard Time";

    /// <summary>Fuso do estabelecimento. Resolve o ID IANA ou o equivalente Windows.</summary>
    public static TimeZoneInfo TimeZone { get; } = ResolverTimeZone();

    /// <summary>Data e hora corrente no fuso local.</summary>
    public static DateTime Agora => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

    /// <summary>
    /// Intervalo UTC [início, fim) que corresponde ao dia civil local de hoje.
    /// Use nas queries: os timestamps no banco estão em UTC.
    /// </summary>
    public static (DateTime InicioUtc, DateTime FimUtc) IntervaloDeHojeUtc() =>
        IntervaloDoDiaUtc(Agora.Date);

    /// <summary>
    /// Intervalo UTC [início, fim) que corresponde a um dia civil local.
    /// </summary>
    /// <param name="diaLocal">Data no fuso local; o componente de hora é ignorado.</param>
    public static (DateTime InicioUtc, DateTime FimUtc) IntervaloDoDiaUtc(DateTime diaLocal)
    {
        var inicioLocal = DateTime.SpecifyKind(diaLocal.Date, DateTimeKind.Unspecified);
        var fimLocal = inicioLocal.AddDays(1);

        return (
            TimeZoneInfo.ConvertTimeToUtc(inicioLocal, TimeZone),
            TimeZoneInfo.ConvertTimeToUtc(fimLocal, TimeZone)
        );
    }

    private static TimeZoneInfo ResolverTimeZone()
    {
        // .NET 6+ aceita IDs IANA no Windows via ICU, mas o fallback cobre
        // ambientes com globalization-invariant ou ICU indisponível.
        try { return TimeZoneInfo.FindSystemTimeZoneById(IanaId); }
        catch (TimeZoneNotFoundException) { }
        catch (InvalidTimeZoneException) { }

        try { return TimeZoneInfo.FindSystemTimeZoneById(WindowsId); }
        catch (TimeZoneNotFoundException) { }
        catch (InvalidTimeZoneException) { }

        // Último recurso: offset fixo de Brasília (sem horário de verão, abolido em 2019).
        return TimeZoneInfo.CreateCustomTimeZone("PDV-BRT", TimeSpan.FromHours(-3), "Horário de Brasília", "BRT");
    }
}
