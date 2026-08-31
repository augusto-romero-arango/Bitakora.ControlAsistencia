namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Evento de event sourcing que registra el inicio de una vinculacion del colaborador (codigo +
/// fecha de inicio). Se persiste en el stream de ColaboradorAggregateRoot, junto con
/// ColaboradorRegistrado, en un solo commit.
/// </summary>
/// <remarks>
/// Issue #330: payload plano, sin invariantes propias en este corte -- la validacion de "requerido"
/// vive en la capa 1 (RegistrarColaboradorValidator); las invariantes de negocio de la vinculacion
/// (maximo una vigente, no-solape) se ejercen en issues futuros (#349+). Un evento no conoce su
/// comando (CA-ADR-0029): el reingreso (#350) reutilizara este mismo evento.
/// No necesita ConfigurarSerializacion: tipos primitivos (string, DateOnly), STJ lo reconstruye sin
/// ayuda -- mismo criterio que ProgramacionTurnoSolicitada.
/// Issue #520: CodigoSede opcional, evolucion aditiva -- la vinculacion nace con su sede si el
/// comando la trae. Nullable con default null: los eventos historicos sin este campo deserializan
/// con sede null (MEF-ADR-0005), mismo default que un reingreso sin sede.
/// </remarks>
public sealed record VinculacionIniciada(string Codigo, DateOnly FechaInicio, string? CodigoSede = null);
