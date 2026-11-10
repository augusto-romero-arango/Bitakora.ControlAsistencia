namespace Bitakora.ControlAsistencia.ReadModels.Colaboradores;

/// <summary>
/// Etiqueta dinamica (categoria:valor) tal como se muestra en la ficha -- forma ORIGINAL para
/// display, sin normalizar.
/// </summary>
public sealed record EtiquetaFicha(string Categoria, string Valor);

/// <summary>
/// Ficha de un colaborador por identificacion. Incluye colaboradores no vigentes porque es la base
/// del flujo de reingreso: se consulta antes de decidir registrar o reingresar.
/// </summary>
/// <remarks>
/// Id es el stream key "{Tipo}-{Numero}" (ej. "CC-123456"), contrato de Identificacion.ToString().
///
/// CodigoColaborador, VigenteDesde, VigenteHasta y CodigoSede reflejan la ULTIMA vinculacion, sin
/// historial. Un reingreso los sobrescribe y VACIA ambas estructuras de etiquetas ("reingreso nace
/// limpio").
///
/// VigenteHasta usa <see cref="CentinelaVigenciaAbierta"/> con la vinculacion abierta, para filtrar
/// por rango; jamas sale por la API (ObtenerFichaColaborador lo traduce a vacio). Sin booleano de
/// vigencia: el preaviso la voltea sin evento de reloj, asi que se evalua en la consulta.
///
/// EtiquetasNormalizadas sirve al filtrado por containment JSONB; Etiquetas guarda las formas
/// originales para presentacion. Un valor por categoria normalizada.
///
/// CodigoSede es solo el codigo (null = la vinculacion vigente no tiene sede): el nombre lo resuelve
/// el consumidor contra FichaSede, asi un rename de sede no toca esta ficha.
/// </remarks>
[MaterializadoPorProyeccion]
public sealed record FichaColaborador(
    string Id,
    string NombreCompleto,
    string CodigoColaborador,
    DateOnly VigenteDesde,
    DateOnly VigenteHasta,
    IReadOnlyList<EtiquetaFicha> Etiquetas,
    IReadOnlyDictionary<string, string> EtiquetasNormalizadas,
    string? CodigoSede = null,
    Guid? JornadaId = null)
{
    /// <summary>
    /// Valor de <see cref="VigenteHasta"/> que significa "vinculacion abierta" (sin terminacion
    /// registrada).
    /// </summary>
    /// <remarks>
    /// Vive con la vista porque la comparten el worker que la escribe y el Function App que la
    /// traduce a vacio: declarada por lado, una divergencia compilaria, pasaria los tests unitarios y
    /// solo afloraria en dev.
    /// </remarks>
    public static readonly DateOnly CentinelaVigenciaAbierta = new(9999, 12, 31);
}
