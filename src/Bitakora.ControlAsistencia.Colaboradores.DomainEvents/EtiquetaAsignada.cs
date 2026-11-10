namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Asignacion de una etiqueta dinamica a la vinculacion vigente de un colaborador.
/// </summary>
/// <remarks>
/// Siempre es el estado final de la categoria: se emite igual al crearla que al sobrescribir su
/// valor, sin distinguir los dos casos.
/// </remarks>
public sealed record EtiquetaAsignada(Etiqueta Etiqueta);
