namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public sealed class AmbiguedadDeMarcacionesResuelta
{
    public string Id { get; private set; } = null!;
    public DiaVecino Vecino { get; private set; }
    public IReadOnlyList<MarcacionDecidida> Marcaciones { get; private set; } = [];

    private AmbiguedadDeMarcacionesResuelta(string id, DiaVecino vecino, IReadOnlyList<MarcacionDecidida> marcaciones)
    {
        Id = id;
        Vecino = vecino;
        Marcaciones = marcaciones;
    }

    private AmbiguedadDeMarcacionesResuelta() { }

    public static AmbiguedadDeMarcacionesResuelta Crear(
        string id, DiaVecino vecino, IReadOnlyList<MarcacionDecidida> marcaciones) =>
        new(id, vecino, marcaciones);
}
