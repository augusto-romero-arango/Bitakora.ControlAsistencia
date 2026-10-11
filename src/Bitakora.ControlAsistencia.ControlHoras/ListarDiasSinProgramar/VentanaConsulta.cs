namespace Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

public readonly record struct VentanaAplicada(DateOnly Hasta, bool RangoRecortado);

public static class VentanaConsulta
{
    public const int CotaDias = 35;

    public static VentanaAplicada Recortar(DateOnly desde, DateOnly hasta) => throw new NotImplementedException();
}
