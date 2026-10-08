using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

public record DisenoDeTurnoActualizado(
    Guid TurnoId,
    long Version,
    string Nombre,
    bool EsDescanso,
    IReadOnlyList<DetalleFranjaOrdinaria> Franjas,
    bool Retirado) : IPrivateEvent
{
    public virtual bool Equals(DisenoDeTurnoActualizado? other)
    {
        if (other is null) return false;
        return TurnoId == other.TurnoId
            && Version == other.Version
            && Nombre == other.Nombre
            && EsDescanso == other.EsDescanso
            && Retirado == other.Retirado
            && Franjas.SequenceEqual(other.Franjas);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TurnoId);
        hash.Add(Version);
        hash.Add(Nombre);
        hash.Add(EsDescanso);
        hash.Add(Retirado);
        foreach (var f in Franjas) hash.Add(f);
        return hash.ToHashCode();
    }
}
