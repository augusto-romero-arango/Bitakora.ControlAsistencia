namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class LimitesJornadaModificados
{
    public Guid JornadaId { get; private set; }
    public LimitesJornada Limites { get; private set; } = null!;

    private LimitesJornadaModificados() { }
    private LimitesJornadaModificados(Guid jornadaId, LimitesJornada limites)
    {
        JornadaId = jornadaId;
        Limites = limites;
    }

    public static LimitesJornadaModificados Crear(Guid jornadaId, LimitesJornada limites) => new(jornadaId, limites);
}
