namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Issue #277: gemela de IdentidadEventosProgramacion. Lista de los tipos de evento que SE
// PERSISTEN en el event store de ControlHoras -- el mismo criterio de inclusion de este
// ensamblado (CA-ADR-0029). MarcacionRegistrada implementa ademas IPrivateEvent (cruza el ASB
// interno del BC), pero eso no la excluye: tambien se persiste en el stream de
// ControlDiarioAggregateRoot, asi que entra en esta lista por ese motivo. Los eventos que SOLO
// cruzan el bus (p.ej. DiaCalculado, ProgramacionTurnoDiarioSolicitada) no entran: nunca pasan
// por el EventGraph de Marten.
//
// Registrar estos tipos via Events.AddEventTypes NO declara su alias -- Marten lo sigue
// derivando del nombre de clase (JasperFx.Events.EventTypeExtensions.GetSmarterEventTypeName /
// GetEventTypeName, Marten 9.12 + JasperFx.Events 2.18.1, ambos resuelven igual para tipos
// top-level no genericos). Lo unico que AddEventTypes garantiza es que el mapping exista antes
// de la primera lectura del proceso, en vez de depender de que un append lo haya poblado
// (issue #237 seccion "Consecuencia asumida").
//
// No duplica ConfiguracionSerializacionControlHoras: esa cubre los tipos ricos que necesitan
// ConfigurarSerializacion; esta cubre los tipos persistidos. Ninguna es subconjunto de la otra.
//
// STUB de fase roja (issue #277, test-writer): lista vacia a proposito. Sin los tres tipos aqui,
// IdentidadEventosControlHorasTests (CA-1), AliasEventosControlHorasTests (CA-5) y las guardas de
// ComposicionServiciosTests / ConfiguracionMartenProjectionsTests (CA-2/CA-3/CA-4) fallan. El
// implementer completa esta lista con los tres tipos reales.
public static class IdentidadEventosControlHoras
{
    public static IReadOnlyList<Type> TiposPersistidos { get; } = [];
}
