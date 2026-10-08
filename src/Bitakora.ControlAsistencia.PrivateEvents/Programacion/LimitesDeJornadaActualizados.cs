using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

public sealed class LimitesDeJornadaActualizados : IPrivateEvent
{
    public Guid JornadaId { get; private set; }
    public long Version { get; private set; }
    public int HorasSemanalesEnMinutos { get; private set; }
    public int TopeDiarioEnMinutos { get; private set; }
    public int MinimoDiarioEnMinutos { get; private set; }
    public int DiasDescansoPorSemana { get; private set; }

    public LimitesDeJornadaActualizados(
        Guid jornadaId, long version, int horasSemanalesEnMinutos, int topeDiarioEnMinutos,
        int minimoDiarioEnMinutos, int diasDescansoPorSemana)
    {
        JornadaId = jornadaId;
        Version = version;
        HorasSemanalesEnMinutos = horasSemanalesEnMinutos;
        TopeDiarioEnMinutos = topeDiarioEnMinutos;
        MinimoDiarioEnMinutos = minimoDiarioEnMinutos;
        DiasDescansoPorSemana = diasDescansoPorSemana;
    }

    private LimitesDeJornadaActualizados() { }
}
