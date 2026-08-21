using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Nunca se persiste: IdentidadEventosControlHoras lo excluye a proposito.
//
// CodigoColaborador viaja top-level y siempre presente, tambien cuando Colaborador es null (dia
// nacido solo por marcacion, sin turno): el consumidor arma "dc:{codigo}:{yyyyMMdd}" con el. Moverlo
// dentro de Colaborador reintroduce el defecto que este campo corrige.
//
// NombreTurno + Franjas son la senal estructural del plan, sin enum: null = sin programacion;
// nombre + Franjas vacia = descanso programado; nombre + Franjas >= 1 = jornada valida. Un dia sin
// jornada valida no tiene depuracion (Franjas y HorasPorConcepto vacios, Marcaciones cruda): la
// anomalia la deriva el receptor de esa combinacion, no un campo explicito del payload.
//
// Marcaciones viaja completa y en orden cronologico ascendente: es contrato del evento, no reflejo
// del orden de llegada al aggregate. Es la unica evidencia de las anomalias de un dia sin jornada
// valida, y el consumidor no puede ir a buscarlas a otro aggregate.
public record DiaDepurado(
    string CodigoColaborador,
    DateOnly Fecha,
    ResumenColaborador? Colaborador,
    string? NombreTurno,
    IReadOnlyList<FranjaDepurada> Franjas,
    IReadOnlyList<MarcacionDelDia> Marcaciones,
    HorasDiscriminadas HorasDiscriminadas) : IPrivateEvent
{
    // El record por defecto compara Franjas/Marcaciones por referencia; estos overrides las comparan
    // por valor. Todo campo nuevo del record debe sumarse a ambos a mano (MEF-ADR-0012, nota sobre
    // equality: un record con colecciones promete una igualdad que el compilador no genera).
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
