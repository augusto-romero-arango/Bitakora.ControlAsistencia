namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Filtro tipado del body de ListarAsistenciasDiarias (issue #427, verbo QUERY -- MEF-ADR-0042).
/// Los tres campos son AND por defecto y los tres son obligatorios (issue #427, "Filtro"):
/// <c>CodigoColaborador</c> ausente/vacio, o cualquiera de las dos fechas ausente, o un rango
/// invertido (<c>DesdeFecha</c> posterior a <c>HastaFecha</c>) son 422 -- nunca 400: el JSON esta
/// bien formado, pero su contenido no es procesable (RFC 10008 seccion 2.1).
///
/// Todos nullable a proposito: permite distinguir "el campo no vino" (422 con mensaje propio) de
/// "el campo vino con un valor invalido para el tipo" (400 por el catch de JsonException del
/// endpoint, antes de llegar a este record).
/// </summary>
public sealed record FiltroListarAsistenciasDiarias(
    string? CodigoColaborador,
    DateOnly? DesdeFecha,
    DateOnly? HastaFecha);
