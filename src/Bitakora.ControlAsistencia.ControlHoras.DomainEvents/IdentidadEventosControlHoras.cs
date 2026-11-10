namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Tipos de evento que se persisten en el event store de ControlHoras (CA-ADR-0029). Los que solo
// cruzan el bus (p.ej. DiaDepurado, RegistroDeMarcacionCreado) no entran: nunca pasan por el
// EventGraph de Marten.
//
// Events.AddEventTypes no declara el alias: Marten lo sigue derivando del nombre de clase
// (Marten 9.12 + JasperFx.Events 2.18.1). Solo garantiza que el mapping exista antes de la primera
// lectura del proceso, en vez de depender de que un append lo haya poblado.
//
// No duplica ConfiguracionSerializacionControlHoras: esa cubre los tipos que necesitan
// ConfigurarSerializacion; esta, los persistidos. Ninguna es subconjunto de la otra.
public static class IdentidadEventosControlHoras
{
    public static IReadOnlyList<Type> TiposPersistidos { get; } =
    [
        typeof(MarcacionRegistrada),
        typeof(MarcacionAdicionada),
        typeof(TurnoDiarioAsignado),
        typeof(DepuracionDiaRecibida),
        typeof(SedeDeMarcacionIdentificada),
        typeof(DiaAprobado),
        typeof(DepuracionPosAprobacionRecibida),
        typeof(TurnoDiarioCancelado),
        typeof(AusenciaDiariaAsignada),
        typeof(CancelacionAusenciaDiariaRegistrada),
        typeof(CoberturaVecinaActualizada),
        typeof(CierreDeLaVisperaActualizado),
        typeof(AmbiguedadDeMarcacionesResuelta),
        typeof(ResolucionDeAmbiguedadDescartada)
    ];
}
