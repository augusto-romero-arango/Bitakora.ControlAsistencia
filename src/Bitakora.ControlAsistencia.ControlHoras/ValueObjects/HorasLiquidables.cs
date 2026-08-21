namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// Issue #424: la frontera de idiomas del BC. El mundo maquina (ControlDiario) habla minutos enteros;
// desde DiaDepurado hacia el mundo humano (aprobacion, vistas, nomina) se habla horas liquidables.
// Unico punto de conversion del BC -- ninguna otra clase debe dividir minutos entre 60 a mano
// (decision del humano, 2026-08-20): la precision queda centralizada aqui, nunca configurable por
// vista (doctrina del glosario).
public static class HorasLiquidables
{
    // Constante NOMBRADA de precision -- NUNCA un literal suelto en la conversion. Cambiar la
    // precision de negocio es cambiar este unico valor.
    private const int PosicionesDecimales = 2;

    // AwayFromZero: sin midpoints exactos alcanzables con /60 a 2 decimales (nota del issue), el
    // criterio de desempate es irrelevante en la practica; se deja explicito por si esa premisa cambia.
    public static decimal DesdeMinutos(int minutos) =>
        Math.Round(minutos / 60m, PosicionesDecimales, MidpointRounding.AwayFromZero);
}
