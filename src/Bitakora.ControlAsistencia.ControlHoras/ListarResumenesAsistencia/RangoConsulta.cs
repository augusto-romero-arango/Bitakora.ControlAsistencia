namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>
/// Rango efectivamente aplicado tras acotar el pedido a <see cref="RangoConsulta.CotaDias"/>.
/// </summary>
public readonly record struct RangoAplicado(DateOnly HastaAplicado, bool RangoRecortado);

/// <summary>
/// Recorte del rango de consulta: SIEMPRE hacia adelante desde <c>desde</c> -- nunca hacia atras
/// desde <c>hasta</c> ni relativo a la fecha de hoy, que haria que la misma consulta devolviera
/// datos distintos segun el dia en que se ejecuta.
///
/// Tercera copia deliberada de esta politica en ControlHoras (ListarTurnosVigentes,
/// ListarAsistenciasDiarias, esta): cada listado es dueno de su propia cota y puede divergir
/// (MEF-ADR-0018). Cambiar CotaDias aqui NO alcanza a las otras dos.
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
