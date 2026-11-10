namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Terna de identidad del colaborador que persiste TurnoDiarioAsignado (payload por rol,
/// MEF-ADR-0039 decision #6).
///
/// Identificacion llega con el contrato del maestro Colaboradores ("{Tipo}-{Numero}", nunca
/// aplanada) y NombreCompleto ya concatenado: nadie compone nada a partir de este payload.
///
/// Comparte forma con ResumenColaborador pero se mantienen separados (MEF-ADR-0018): unificarlos
/// amarra la evolucion de un evento a la del otro.
///
/// No va en IdentidadEventosControlHoras.TiposPersistidos: es payload anidado, sin alias ni $type.
/// Por eso reducirle campos exige purgar los streams en el mismo despliegue: releidos con el tipo
/// reducido, los eventos escritos con el quinteto dejan Identificacion/NombreCompleto en null SIN
/// excepcion (MEF-ADR-0036 seccion 5 por analogia).
/// </summary>
public record ColaboradorProgramado(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
