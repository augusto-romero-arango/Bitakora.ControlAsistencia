namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Datos de entrada de una franja para TurnoCreado.Crear(); el payload del evento es FranjaOrdinaria.
// Existe para que el factory no dependa del comando CrearTurno, que vive en la Function App y cerraria
// un ciclo de referencias; el comando se convierte con CrearTurno.ToDatosFranjas().
// DiaOffsetFin 0 = inferir (+1 si Fin < Inicio); explicito habilita la franja de 24 h exactas.
public record DatosFranja(
    TimeOnly Inicio,
    TimeOnly Fin,
    List<(TimeOnly inicio, TimeOnly fin)> Descansos,
    List<(TimeOnly inicio, TimeOnly fin)> Extras,
    SedeProgramada? Sede = null,
    int DiaOffsetFin = 0);
