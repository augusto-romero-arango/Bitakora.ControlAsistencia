namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

public readonly record struct RangoAplicado(DateOnly HastaAplicado, bool RangoRecortado);

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
