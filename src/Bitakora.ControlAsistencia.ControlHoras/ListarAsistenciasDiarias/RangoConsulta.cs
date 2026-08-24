namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Rango efectivamente aplicado tras acotar el pedido a <see cref="RangoConsulta.CotaDias"/>.
/// </summary>
public readonly record struct RangoAplicado(DateOnly HastaAplicado, bool RangoRecortado);

/// <summary>
/// Recorte del rango de consulta: SIEMPRE hacia adelante desde <c>desde</c> -- nunca hacia atras
/// desde <c>hasta</c> ni relativo a la fecha de hoy, que haria que la misma consulta devolviera
/// datos distintos segun el dia en que se ejecuta.
///
/// Duplicada a proposito de ListarTurnosVigentes.RangoConsulta: segunda aparicion de la politica,
/// tolerada por MEF-ADR-0018 (Rule of Three). Un tercer consumidor decide si se extrae a un lugar
/// comun del dominio; reusar la clase del otro feature folder no es la salida.
/// </summary>
public static class RangoConsulta
{
    /// <summary>Cota maxima del rango, en dias, INCLUSIVE: desde y desde + 30 dias caben.</summary>
    public const int CotaDias = 31;

    public static RangoAplicado Recortar(DateOnly desde, DateOnly hasta)
    {
        var hastaMaxima = desde.AddDays(CotaDias - 1);

        return hasta > hastaMaxima
            ? new RangoAplicado(hastaMaxima, true)
            : new RangoAplicado(hasta, false);
    }
}
