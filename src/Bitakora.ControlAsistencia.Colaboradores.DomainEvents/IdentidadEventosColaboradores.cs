namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

// Tipos de evento que se persisten en el event store de Colaboradores; los que solo cruzan el bus
// no van aqui.
//
// Registrarlos con Events.AddEventTypes NO declara su alias -- Marten lo sigue derivando del nombre
// de clase (JasperFx.Events.EventTypeExtensions.GetSmarterEventTypeName). Solo garantiza que el
// mapping exista antes de la primera lectura del proceso, sin depender de que un append lo pueble.
public static class IdentidadEventosColaboradores
{
    public static IReadOnlyList<Type> TiposPersistidos { get; } =
    [
        typeof(ColaboradorRegistrado),
        typeof(VinculacionIniciada),
        typeof(VinculacionTerminada),
        typeof(NombresCorregidos),
        typeof(FechaInicioVinculacionCorregida),
        typeof(TerminacionAnulada),
        typeof(EtiquetaAsignada),
        typeof(EtiquetaRetirada),
        typeof(SedeAsignada),
        typeof(JornadaAsignada),
    ];
}
