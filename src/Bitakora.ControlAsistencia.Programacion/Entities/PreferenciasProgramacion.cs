using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class PreferenciasProgramacion : AggregateRoot
{
    private const string PrefijoStream = "pp";

    private Guid _jornadaPredeterminadaId;

    internal static string ComputarStreamId(string tenantId) => $"{PrefijoStream}:{tenantId}";

    internal static PreferenciasProgramacion Iniciar(JornadaPredeterminadaAsignada evento)
    {
        var preferencias = new PreferenciasProgramacion();
        preferencias._uncommittedEvents.Add(evento);
        preferencias.Apply(evento);
        return preferencias;
    }

    internal Guid JornadaPredeterminada() => _jornadaPredeterminadaId;

    internal ResultadoAsignarJornadaPredeterminada AsignarJornadaPredeterminada(Guid jornadaId) =>
        throw new NotImplementedException();

    public void Apply(JornadaPredeterminadaAsignada evento) => _jornadaPredeterminadaId = evento.JornadaId;
}
