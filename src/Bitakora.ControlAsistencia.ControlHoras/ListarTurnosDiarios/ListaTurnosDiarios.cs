using Bitakora.ControlAsistencia.ControlHoras.ObtenerTurnoDiario;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosDiarios;

/// <summary>
/// Envelope de respuesta de ListarTurnosDiarios (issue #290). NO es un read model -- vive en el
/// Function App (ControlHoras), no en ReadModels: es el contrato HTTP de esta query, con el recorte
/// de rango ya aplicado (CA-3/CA-4) y el seam donde aterrizan paginacion y tope de resultados sin
/// romper clientes existentes el dia que se agreguen (Rule of Three, MEF-ADR-0018).
///
/// <c>Turnos</c> reutiliza el mismo <see cref="TurnoDiarioRespuesta"/> que devuelve ObtenerTurnoDiario
/// (#289), sin el <c>Id</c> del documento (CA-6) -- no se redeclara un DTO nuevo para el mismo
/// concepto.
/// </summary>
public sealed record ListaTurnosDiarios(
    DateOnly DesdeAplicado,
    DateOnly HastaAplicado,
    bool RangoRecortado,
    IReadOnlyList<TurnoDiarioRespuesta> Turnos);
