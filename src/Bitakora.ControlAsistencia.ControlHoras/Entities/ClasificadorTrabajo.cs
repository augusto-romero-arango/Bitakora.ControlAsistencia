// Issue #135: Clasificar intervalos trabajados segun programacion de la franja.
// Capa 3 (Parte C) del calculo de desglose de horas: integra la segmentacion (#115) y
// la clasificacion banda+tipo (#134) con la programacion del turno (DetalleFranjaOrdinaria):
// descansos, extras programadas y excedente (salida mas alla del fin programado).
// Trabaja siempre en MomentoDelDia - no construye ningun DateTime en este nivel.
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

/// <summary>
/// Produce la lista de IntervaloClasificado que describe cada minuto trabajado con su concepto legal,
/// dado el contexto de una franja programada. La entrada efectiva es max(trabajado.Inicio, inicioFranja):
/// los minutos antes del inicio programado no aparecen en el desglose.
/// </summary>
public static class ClasificadorTrabajo
{
    /// <summary>
    /// Clasifica el intervalo trabajado contra la programacion de la franja.
    /// fechaAncla se usa solo para consultar esFestivo al clasificar el tipo de dia de cada segmento.
    /// Precondicion: trabajado es un intervalo valido (no vacio); la franja anomala se maneja en #136.
    /// </summary>
    public static IReadOnlyList<IntervaloClasificado> Clasificar(
        DetalleFranjaOrdinaria programada,
        IntervaloTemporal trabajado,
        DateOnly fechaAncla,
        Func<DateOnly, bool> esFestivo)
        => throw new NotImplementedException();
}
