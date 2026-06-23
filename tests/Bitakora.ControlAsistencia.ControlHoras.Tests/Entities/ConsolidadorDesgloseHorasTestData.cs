// Issue #116: Helpers de construccion compartidos por las familias de tests del
// ConsolidadorDesgloseHoras (familias 1-6). Cada familia vive en su propia clase y
// consume estos helpers via `using static`, evitando duplicarlos en seis archivos.
//
// Convencion de datos: las familias 1-5 construyen DesgloseFranja a mano con
// IntervaloClasificado concretos en lugar de pasar por ControlFranja.CalcularDesglose,
// para focalizar los tests en el algoritmo de consolidacion (la clasificacion ya esta
// cubierta en #135 + #136). La familia 6 (puente) usa el codigo real de #136.
//
// Todas las aserciones se hacen en MomentoDelDia / IntervaloTemporal / DetalleRetardo /
// Concepto. DetalleRetardo (#114) solo expone RetardoNeto publicamente (ADR-0015,
// Tell-don't-Ask): los minutos retardados/compensados son detalle interno, asi que el
// retardo consolidado se verifica construyendo el DetalleRetardo esperado via
// DetalleRetardo.Crear(...) y comparando por igualdad (que internamente compara minutos
// e intervalos), mas RetardoNeto.

using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

internal static class ConsolidadorDesgloseHorasTestData
{
    // Momento del dia: hora[:minuto] con offset opcional de dia.
    public static MomentoDelDia M(int hora, int minuto = 0, int diaOffset = 0) =>
        new(new TimeOnly(hora, minuto), diaOffset);

    // Intervalo temporal entre dos momentos.
    public static IntervaloTemporal I(MomentoDelDia inicio, MomentoDelDia fin) =>
        IntervaloTemporal.Crear(inicio, fin);

    // Intervalo clasificado con un concepto legal.
    public static IntervaloClasificado Clasif(MomentoDelDia inicio, MomentoDelDia fin, Concepto concepto) =>
        new(IntervaloTemporal.Crear(inicio, fin), concepto);

    // Franja programada plana (sin descansos ni extras programadas declarados): solo
    // viaja en el DesgloseFranja para que el consolidador la preserve en DesglosePorFranja.
    // El detalle real de lo trabajado vive en los IntervaloClasificado de cada franja.
    public static DetalleFranjaOrdinaria Programada(int horaInicio, int horaFin, int diaOffsetFin = 0) =>
        new(new TimeOnly(horaInicio, 0), new TimeOnly(horaFin, 0), diaOffsetFin, [], []);

    // DetalleRetardo de una franja, a partir de sus intervalos retardados y compensados.
    public static DetalleRetardo Retardo(
        IReadOnlyList<IntervaloTemporal> retardado,
        IReadOnlyList<IntervaloTemporal> compensado) =>
        DetalleRetardo.Crear(retardado, compensado);

    // DesgloseFranja sintetico: programada + intervalos clasificados + detalle de retardo.
    public static DesgloseFranja Desglose(
        DetalleFranjaOrdinaria programada,
        IReadOnlyList<IntervaloClasificado> intervalos,
        DetalleRetardo retardo) =>
        new(programada, intervalos, retardo);
}
