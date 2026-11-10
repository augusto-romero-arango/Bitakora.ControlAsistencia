using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Nunca se persiste: IdentidadEventosControlHoras lo excluye a proposito.
//
// CodigoColaborador viaja top-level y siempre, tambien con Colaborador null (dia nacido solo por
// marcacion): el consumidor arma "dc:{codigo}:{yyyyMMdd}" con el. No moverlo dentro de Colaborador.
//
// NombreTurno + Franjas son la senal del plan: null = sin programacion; nombre + Franjas vacia =
// descanso programado; nombre + Franjas >= 1 = jornada valida. Sin jornada valida no hay depuracion
// (Franjas y HorasPorConcepto vacios, Marcaciones cruda) y el receptor deriva la anomalia de esa
// combinacion.
//
// Marcaciones viaja completa y en orden cronologico ascendente (contrato del evento): es la unica
// evidencia de las anomalias de un dia sin jornada valida.
public record DiaDepurado(
    string CodigoColaborador,
    DateOnly Fecha,
    ResumenColaborador? Colaborador,
    string? NombreTurno,
    IReadOnlyList<FranjaDepurada> Franjas,
    IReadOnlyList<MarcacionDelDia> Marcaciones,
    HorasDiscriminadas HorasDiscriminadas,
    string? MotivoAusencia = null,
    long? VersionControlDiario = null) : IPrivateEvent
{
    // Equals/GetHashCode comparan Franjas/Marcaciones por valor: todo campo nuevo del record debe sumarse
    // a ambos a mano.
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
            && HorasDiscriminadas == other.HorasDiscriminadas
            && MotivoAusencia == other.MotivoAusencia
            && VersionControlDiario == other.VersionControlDiario;
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
        hash.Add(MotivoAusencia);
        hash.Add(VersionControlDiario);
        return hash.ToHashCode();
    }
}
