namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// La frontera de idiomas del BC: el mundo maquina (ControlDiario) habla minutos enteros; desde
// DiaDepurado hacia el mundo humano (aprobacion, vistas, nomina) se habla horas liquidables.
// Unico punto de conversion del BC: ninguna otra clase divide minutos entre 60 a mano, y la precision
// no es parametro del llamador ni configurable por vista.
public static class HorasLiquidables
{
    private const int PosicionesDecimales = 2;

    // AwayFromZero explicito: con /60 a 2 decimales no hay midpoints alcanzables, asi que el criterio
    // de desempate hoy no cambia ningun resultado. Si la precision sube, empieza a importar.
    public static decimal DesdeMinutos(int minutos) =>
        Math.Round(minutos / 60m, PosicionesDecimales, MidpointRounding.AwayFromZero);
}
