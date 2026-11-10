namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;

public readonly record struct RangoAplicado(DateOnly HastaAplicado, bool RangoRecortado);

/// <summary>
/// Recorte del rango de consulta siempre hacia adelante desde <c>desde</c>. Duplicado a proposito de
/// la misma politica en ControlHoras (ListarTurnosVigentes, ListarAsistenciasDiarias): extraerla
/// cruzaria dominios.
/// </summary>
public static class RangoConsulta
{
    public const int CotaDias = 35;

    public static RangoAplicado Recortar(DateOnly desde, DateOnly hasta)
    {
        var hastaMaxima = desde.AddDays(CotaDias - 1);
        return hasta > hastaMaxima
            ? new RangoAplicado(hastaMaxima, true)
            : new RangoAplicado(hasta, false);
    }
}
