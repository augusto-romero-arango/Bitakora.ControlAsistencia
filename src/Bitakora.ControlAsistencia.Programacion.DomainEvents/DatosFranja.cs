namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #237: datos de entrada de una franja ordinaria para TurnoCreado.Crear().
// No es payload del evento -- el payload es FranjaOrdinaria, el VO rico. Existe para que
// el factory no dependa del comando CrearTurno, que vive en la Function App y dejaria el
// grafo de referencias en ciclo. El comando se convierte con CrearTurno.ToDatosFranjas().
// Issue #335: Sede es campo aditivo y opcional -- se propaga a FranjaOrdinaria.Crear() dentro de
// TurnoCreado.Crear(). El mapeo comando -> DatosFranja lo hace CrearTurno.ToDatosFranjas().
public record DatosFranja(
    TimeOnly Inicio,
    TimeOnly Fin,
    List<(TimeOnly inicio, TimeOnly fin)> Descansos,
    List<(TimeOnly inicio, TimeOnly fin)> Extras,
    SedeProgramada? Sede = null);
