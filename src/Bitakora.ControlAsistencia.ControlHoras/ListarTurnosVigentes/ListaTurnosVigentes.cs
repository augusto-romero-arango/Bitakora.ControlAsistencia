using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

/// <summary>
/// Envelope de respuesta de ListarTurnosVigentes (issue #329). NO es un read model -- vive en el
/// Function App (ControlHoras), no en ReadModels: es el contrato HTTP de esta query, con el recorte
/// de rango ya aplicado (CA-3) y el seam donde aterrizan paginacion y tope de resultados sin romper
/// clientes existentes el dia que se agreguen (Rule of Three, MEF-ADR-0018), mismo patron que
/// <c>ListarTurnosDiarios.ListaTurnosDiarios</c> (issue #290).
///
/// <c>Turnos</c> reutiliza <see cref="TurnoVigente"/> tal cual la materializa la proyeccion (#328),
/// ancla <c>Id</c> y <c>Bloques</c> incluidos -- decision de entrevista del issue #329 ("Notas
/// tecnicas"): una sola proyeccion sirve grilla y calendario; el recorte resumido/detallado, si
/// algun dia se necesita, aterriza en un DTO nuevo (Rule of Three), no en este.
/// </summary>
public sealed record ListaTurnosVigentes(
    DateOnly DesdeAplicado,
    DateOnly HastaAplicado,
    bool RangoRecortado,
    IReadOnlyList<TurnoVigente> Turnos);
