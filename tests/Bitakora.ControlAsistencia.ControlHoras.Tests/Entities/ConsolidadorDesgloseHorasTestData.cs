// Helpers compartidos por las familias de tests de ConsolidadorDesgloseHoras. Las familias construyen
// DesgloseFranja a mano para aislar la consolidacion de la clasificacion; la familia puente usa el
// ControlFranja.CalcularDesglose real. Retardo solo expone RetardoNeto: el retardo consolidado se
// verifica contra un Retardo.Crear(...) esperado, cuya igualdad compara tambien los intervalos.

using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

internal static class ConsolidadorDesgloseHorasTestData
{
    public static MomentoDelDia M(int hora, int minuto = 0, int diaOffset = 0) =>
        new(new TimeOnly(hora, minuto), diaOffset);

    public static IntervaloTemporal I(MomentoDelDia inicio, MomentoDelDia fin) =>
        IntervaloTemporal.Crear(inicio, fin);

    public static IntervaloClasificado Clasif(MomentoDelDia inicio, MomentoDelDia fin, Concepto concepto) =>
        new(IntervaloTemporal.Crear(inicio, fin), concepto);

    // Sin descansos ni extras: solo viaja para que el consolidador la preserve en DesglosePorFranja.
    // Descripcion (dato derivado) no interviene: placeholder "".
    public static FranjaProgramada Programada(int horaInicio, int horaFin, int diaOffsetFin = 0) =>
        new(new TimeOnly(horaInicio, 0), new TimeOnly(horaFin, 0), diaOffsetFin, [], [], "");

    public static Retardo CrearRetardo(
        IReadOnlyList<IntervaloTemporal> retardado,
        IReadOnlyList<IntervaloTemporal> compensado) =>
        Retardo.Crear(retardado, compensado);

    public static DesgloseFranja Desglose(
        FranjaProgramada programada,
        IReadOnlyList<IntervaloClasificado> intervalos,
        Retardo retardo) =>
        new(programada, intervalos, retardo);
}
