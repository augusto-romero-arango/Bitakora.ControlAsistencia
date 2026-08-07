namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

/// <summary>
/// Resultado de aplicar la cota de <see cref="RangoConsulta.CotaDias"/> sobre el rango de fechas
/// pedido a ListarTurnosVigentes (issue #329, CA-3).
/// </summary>
public readonly record struct RangoAplicado(DateOnly HastaAplicado, bool RangoRecortado);

/// <summary>
/// Logica pura de recorte del rango de consulta de ListarTurnosVigentes (issue #329, CA-3),
/// mismo patron que <c>ListarTurnosDiarios.RangoConsulta</c> (issue #290).
///
/// Duplicada a proposito en vez de compartida entre los dos namespaces: cada Function GET es un
/// feature folder propio (skills/projections/naming.md) y, con solo dos consumidores, MEF-ADR-0018
/// (Rule of Three) tolera la duplicacion hasta que un tercer consumidor justifique extraerla.
///
/// El recorte es SIEMPRE hacia adelante desde <c>desde</c> -- nunca relativo a la fecha de hoy
/// (mismo razonamiento que #290, seccion "El envelope, y por que existe"): un recorte relativo a
/// "hoy" haria que la misma consulta devolviera datos distintos segun el dia en que se ejecuta.
///
/// Sin dependencias de Marten/Postgres: se prueba como funcion pura sobre <see cref="DateOnly"/>,
/// sin QuerySession (skills/projections/read-apis.md).
/// </summary>
public static class RangoConsulta
{
    /// <summary>Cota maxima del rango, en dias, INCLUSIVE (CA-3): desde y desde + 30 dias caben.</summary>
    public const int CotaDias = 31;

    public static RangoAplicado Recortar(DateOnly desde, DateOnly hasta)
    {
        // CotaDias es INCLUSIVE: desde + (CotaDias - 1) dias es el limite exacto que todavia no
        // excede la cota (31 dias inclusive = desde + 30 dias).
        var hastaMaxima = desde.AddDays(CotaDias - 1);

        return hasta > hastaMaxima
            ? new RangoAplicado(hastaMaxima, true)
            : new RangoAplicado(hasta, false);
    }
}
