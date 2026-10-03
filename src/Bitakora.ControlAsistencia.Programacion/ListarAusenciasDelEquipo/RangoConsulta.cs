namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;

public readonly record struct RangoAplicado(DateOnly HastaAplicado, bool RangoRecortado);

/// <summary>
/// Recorte del rango de consulta siempre hacia adelante desde <c>desde</c>. Tercera aparicion de
/// la politica (ListarTurnosVigentes, ListarAsistenciasDiarias son de ControlHoras): vive en otro
/// dominio, asi que se duplica a proposito (MEF-ADR-0018); una extraccion cruzaria dominios.
/// </summary>
public static class RangoConsulta
{
    public const int CotaDias = 31;

    public static RangoAplicado Recortar(DateOnly desde, DateOnly hasta)
    {
        var hastaMaxima = desde.AddDays(CotaDias - 1);
        return hasta > hastaMaxima
            ? new RangoAplicado(hastaMaxima, true)
            : new RangoAplicado(hasta, false);
    }
}
