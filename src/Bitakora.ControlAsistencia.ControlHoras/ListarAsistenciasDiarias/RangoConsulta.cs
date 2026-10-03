namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Rango efectivamente aplicado tras acotar el pedido.
/// </summary>
public readonly record struct RangoAplicado(DateOnly HastaAplicado, bool RangoRecortado);

/// <summary>
/// Recorte del rango de consulta: SIEMPRE hacia adelante desde <c>desde</c> -- nunca hacia atras
/// desde <c>hasta</c> ni relativo a la fecha de hoy, que haria que la misma consulta devolviera
/// datos distintos segun el dia en que se ejecuta.
///
/// La cota no se comparte con ListarTurnosVigentes: ambas consultas evolucionan de forma
/// independiente (MEF-ADR-0018).
/// </summary>
public static class RangoConsulta
{
    /// <summary>Cota en dias inclusivos.</summary>
    public const int CotaDias = 35;

    public static RangoAplicado Recortar(DateOnly desde, DateOnly hasta)
    {
        var hastaMaxima = desde.AddDays(CotaDias - 1);

        return hasta > hastaMaxima
            ? new RangoAplicado(hastaMaxima, true)
            : new RangoAplicado(hasta, false);
    }
}
