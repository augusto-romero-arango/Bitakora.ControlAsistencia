namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Issue #455: gemela de IdentidadEventosColaboradores/IdentidadEventosProgramacion/
// IdentidadEventosControlHoras. Lista de los tipos de evento que SE PERSISTEN en el event store de
// Sedes -- el mismo criterio de inclusion de este ensamblado (CA-ADR-0029). El dominio nace sin
// aggregate ni evento propio: la lista se llena a medida que SedeAggregateRoot (desglose #456-#461)
// aplique sus primeros eventos. Los eventos que solo cruzan el bus (PublicEvents/PrivateEvents) no
// entran aqui: nunca pasan por el EventGraph de Marten.
//
// Registrar estos tipos via Events.AddEventTypes NO declara su alias -- Marten lo sigue derivando
// del nombre de clase (JasperFx.Events.EventTypeExtensions.GetSmarterEventTypeName /
// GetEventTypeName). Lo unico que AddEventTypes garantiza es que el mapping exista antes de la
// primera lectura del proceso, en vez de depender de que un append lo haya poblado.
public static class IdentidadEventosSedes
{
    public static IReadOnlyList<Type> TiposPersistidos { get; } = [];
}
