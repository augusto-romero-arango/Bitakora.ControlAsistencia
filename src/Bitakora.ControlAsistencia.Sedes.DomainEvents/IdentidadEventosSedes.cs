namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Solo los tipos que se persisten en el event store de Sedes (CA-ADR-0029); los que solo cruzan el
// bus no van aqui.
//
// Events.AddEventTypes NO declara el alias (Marten lo sigue derivando del nombre de clase): solo
// garantiza que el mapping exista antes de la primera lectura del proceso.
public static class IdentidadEventosSedes
{
    public static IReadOnlyList<Type> TiposPersistidos { get; } =
    [
        typeof(SedeRegistrada),
        typeof(NombreSedeModificado),
        typeof(UbicacionActualizada),
        typeof(CentroDeCostosAsignado),
        typeof(CentroDeCostosRetirado),
        typeof(SedeActivada),
        typeof(SedeDesactivada),
        typeof(DispositivoInstalado),
        typeof(DispositivoRetirado),
    ];
}
