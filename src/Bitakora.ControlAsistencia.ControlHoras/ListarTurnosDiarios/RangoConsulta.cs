namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosDiarios;

/// <summary>
/// Resultado de aplicar la cota de <see cref="RangoConsulta.CotaDias"/> sobre el rango de fechas
/// pedido a ListarTurnosDiarios (issue #290, CA-3/CA-4).
/// </summary>
public readonly record struct RangoAplicado(DateOnly HastaAplicado, bool RangoRecortado);

/// <summary>
/// Logica pura de recorte del rango de consulta de ListarTurnosDiarios (issue #290, CA-3/CA-4).
///
/// El recorte es SIEMPRE hacia adelante desde <c>desde</c> -- nunca relativo a la fecha de hoy
/// (issue #290, seccion "El envelope, y por que existe"): un recorte relativo a "hoy" haria que la
/// misma consulta devolviera datos distintos segun el dia en que se ejecuta, lo que vuelve el
/// endpoint dificil de testear y de cachear.
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
        // CotaDias es INCLUSIVE (CA-4): desde + (CotaDias - 1) dias es el limite exacto que
        // todavia no excede la cota (31 dias inclusive = desde + 30 dias).
        var hastaMaxima = desde.AddDays(CotaDias - 1);

        return hasta > hastaMaxima
            ? new RangoAplicado(hastaMaxima, true)
            : new RangoAplicado(hasta, false);
    }
}
