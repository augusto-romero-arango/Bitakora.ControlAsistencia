namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Sede efectiva donde rige la programacion del turno para el dia solicitado.
/// </summary>
/// <remarks>
/// Issue #331: snapshot de la sede resuelta por el cliente (sede natural del empleado como
/// default silencioso, o la que el Programador indique) -- el servidor NUNCA consulta el maestro
/// de sedes (#330), la verdad queda grabada en el evento. Id es un identificador opaco provisto
/// por el cliente (mismo precedente que EmpleadoId/DispositivoId: puede o no ser un guid). Nombre
/// viaja para que el evento persistido quede autocontenido.
/// Sin Equals custom: todos los campos son string, la igualdad por valor del record por defecto ya
/// es correcta (mismo criterio que Empleado, issue #319).
/// El nombre puro "Sede" queda RESERVADO para el concepto rico del futuro maestro de sedes (#338,
/// direccion/ciudad/dispositivos asociados) -- este record es deliberadamente "Programada" para no
/// hacer squatting de ese nombre.
/// </remarks>
public record SedeProgramada(string Id, string Nombre)
{
    /// <summary>
    /// Una sede referenciada esta completa cuando trae ambos datos: el id opaco del cliente y el
    /// nombre con que quedara autocontenida en el evento.
    /// </summary>
    /// <remarks>
    /// Issue #335: la regla vive aqui y no en quien compone la sede (MEF-ADR-0012, Tell-don't-Ask):
    /// depende solo de datos propios, asi que FranjaOrdinaria.Crear() pregunta en vez de leer Id y
    /// Nombre para decidir por su cuenta.
    /// </remarks>
    public bool EstaCompleta() =>
        !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(Nombre);
}
