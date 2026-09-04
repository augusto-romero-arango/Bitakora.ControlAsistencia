namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #277: lista de los tipos de evento que SE PERSISTEN en el event store de Programacion --
// el mismo criterio de inclusion de este ensamblado (CA-ADR-0029). Los eventos de bus
// (PrivateEvents/PublicEvents) no entran: se deserializan a un tipo fijo por endpoint y nunca
// pasan por el EventGraph de Marten.
//
// Registrar estos tipos via Events.AddEventTypes NO declara su alias -- Marten lo sigue
// derivando del nombre de clase (JasperFx.Events.EventTypeExtensions.GetSmarterEventTypeName /
// GetEventTypeName, Marten 9.12 + JasperFx.Events 2.18.1, ambos resuelven igual para tipos
// top-level no genericos). Lo unico que AddEventTypes garantiza es que el mapping exista antes
// de la primera lectura del proceso, en vez de depender de que un append lo haya poblado
// (issue #237 seccion "Consecuencia asumida").
//
// No duplica ConfiguracionSerializacionProgramacion: esa cubre los tipos ricos que necesitan
// ConfigurarSerializacion (VOs con ctor privado); esta cubre los tipos persistidos (incluye
// ProgramacionTurnoSolicitada, que STJ resuelve solo con su ctor publico). Ninguna es subconjunto
// de la otra.
public static class IdentidadEventosProgramacion
{
    public static IReadOnlyList<Type> TiposPersistidos { get; } =
    [
        typeof(TurnoCreado),
        typeof(ProgramacionTurnoSolicitada),
        typeof(TurnoRetirado),
        typeof(CancelacionProgramacionSolicitada),
        typeof(FranjaAgregada)
    ];
}
