namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Retiro de una etiqueta dinamica de la vinculacion vigente de un colaborador.
/// </summary>
/// <remarks>
/// Solo la categoria normalizada (la clave de Etiqueta.CategoriaNormalizada): al retirar no hay
/// forma original que preservar.
/// </remarks>
public sealed record EtiquetaRetirada(string CategoriaNormalizada);
