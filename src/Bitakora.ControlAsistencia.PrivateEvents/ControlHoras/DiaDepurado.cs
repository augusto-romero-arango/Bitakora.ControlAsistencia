using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Nunca se persiste: IdentidadEventosControlHoras lo excluye a proposito. El nombre DiaCalculado
// queda reservado al aggregate del flujo de aprobacion (#425), que consume este evento.
//
// CodigoColaborador viaja top-level y siempre presente, tambien cuando Colaborador es null (dia
// nacido solo por marcacion, sin turno): el consumidor arma "dc:{codigo}:{yyyyMMdd}" con el. Moverlo
// dentro de Colaborador reintroduce el defecto que este campo corrige.
//
// Issue #424: NombreTurno es la senal ESTRUCTURAL del plan (sin enum): null = sin programacion;
// nombre + Franjas vacia = descanso programado (#423); nombre + Franjas >= 1 = jornada valida. Un dia
// sin jornada valida (sin turno, o turno con cero franjas) no tiene depuracion: Franjas vacia,
// HorasDiscriminadas.HorasPorConcepto vacio y Marcaciones viaja cruda -- la anomalia la deriva el
// receptor de esa combinacion, no un campo explicito (decision 2026-08-20, supersede el extinto #422).
// Marcaciones viaja completa y en orden cronologico ascendente (CONTRATO del evento, no una garantia
// incidental): es la unica evidencia de las anomalias del dia sin jornada valida y la superficie de
// investigacion del Aprobador (#429); DiaCalculado no puede ir a buscarlas (aggregate separado, la
// verdad viaja en el evento).
public record DiaDepurado(
    string CodigoColaborador,
    DateOnly Fecha,
    ResumenColaborador? Colaborador,
    string? NombreTurno,
    IReadOnlyList<FranjaDepurada> Franjas,
    IReadOnlyList<MarcacionDelDia> Marcaciones,
    HorasDiscriminadas HorasDiscriminadas) : IPrivateEvent
{
    // El record por defecto compararia Franjas/Marcaciones por referencia (ADR-0015): con dos
    // colecciones nuevas, Equals/GetHashCode propios comparan por valor (SequenceEqual), precedente
    // TurnoDiario/FranjaProgramada.
    public virtual bool Equals(DiaDepurado? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return CodigoColaborador == other.CodigoColaborador
            && Fecha == other.Fecha
            && Colaborador == other.Colaborador
            && NombreTurno == other.NombreTurno
            && Franjas.SequenceEqual(other.Franjas)
            && Marcaciones.SequenceEqual(other.Marcaciones)
            && HorasDiscriminadas == other.HorasDiscriminadas;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CodigoColaborador);
        hash.Add(Fecha);
        hash.Add(Colaborador);
        hash.Add(NombreTurno);
        foreach (var franja in Franjas) hash.Add(franja);
        foreach (var marcacion in Marcaciones) hash.Add(marcacion);
        hash.Add(HorasDiscriminadas);
        return hash.ToHashCode();
    }
}
