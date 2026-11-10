namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Tipos que SE PERSISTEN en el event store de Programacion. Los eventos de bus no entran: se
// deserializan a un tipo fijo por endpoint y nunca pasan por el EventGraph de Marten.
//
// AddEventTypes NO declara el alias: Marten lo sigue derivando del nombre de clase (Marten 9.12 +
// JasperFx.Events 2.18.1). Solo garantiza que el mapping exista antes de la primera lectura del
// proceso, sin depender de que un append lo haya poblado.
//
// No duplica ConfiguracionSerializacionProgramacion (tipos ricos con ctor privado): ninguna de las dos
// listas es subconjunto de la otra.
public static class IdentidadEventosProgramacion
{
    public static IReadOnlyList<Type> TiposPersistidos { get; } =
    [
        typeof(TurnoCreado),
        typeof(ProgramacionTurnoSolicitada),
        typeof(TurnoRetirado),
        typeof(CancelacionProgramacionSolicitada),
        typeof(FranjaAgregada),
        typeof(DescansoAgregado),
        typeof(ExtraAgregado),
        typeof(FranjaQuitada),
        typeof(DescansoQuitado),
        typeof(ExtraQuitado),
        typeof(SedeDeFranjaAsignada),
        typeof(SedeDeFranjaRetirada),
        typeof(PlantillaDeTurnosCreada),
        typeof(DiaDePlantillaDeTurnosAsignado),
        typeof(PlantillaDeTurnosRetirada),
        typeof(AusenciaProgramada),
        typeof(AusenciaCancelada),
        typeof(JornadaCreada),
        typeof(LimitesJornadaModificados),
        typeof(JornadaPredeterminadaAsignada),
        typeof(JornadaDePlantillaDeTurnosAsignada),
        typeof(JornadaDePlantillaDeTurnosQuitada),
        typeof(AdvertenciasDePlantillaDeTurnosCalculadas),
        typeof(TurnoDePlantillaDeTurnosSincronizado),
        typeof(LimitesDeJornadaDePlantillaDeTurnosSincronizados),
        typeof(JornadaDesactivada),
        typeof(JornadaActivada),
        typeof(VentanaDeEntradaModificada),
        typeof(ProgramacionTurnoAgregadaAlBorrador),
        typeof(ProgramacionPublicada),
        typeof(BorradorDeProgramacionDescartado),
        typeof(ModalidadDeJornadaAsignada),
        typeof(ModalidadDeJornadaQuitada),
        typeof(DescansoAcumuladoDeJornadaMarcado),
        typeof(DescansoAcumuladoDeJornadaDesmarcado)
    ];
}
