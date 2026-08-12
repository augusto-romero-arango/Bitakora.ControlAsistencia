namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #355: resultado de ColaboradorAggregateRoot.RetirarEtiqueta. Mecanismo "declinar con
// resultado" puro (CA-ADR-0030) -- a diferencia de AsignarEtiqueta, retirar NO tiene variante de
// idempotencia silenciosa: retirar una categoria inexistente es un rechazo explicito (CA-4,
// decision de refinamiento 2026-08-11 -- con categorias libres, un typo como "aera" por "area"
// debe aflorar al instante, nunca un 202 silencioso que lo esconda).
// Dos razones de rechazo, evaluables solo con la historia del stream, sin reloj:
//   - CategoriaInexistente: la categoria normalizada no esta en el diccionario de la vinculacion
//     vigente.
//   - VinculacionTerminada: la ULTIMA vinculacion tiene terminacion registrada (incluye un
//     preaviso sin vencer) -- las etiquetas describen la relacion laboral ACTIVA.
// internal: mismo criterio de visibilidad que los resultados hermanos.
internal enum ResultadoRetiroEtiqueta
{
    Exitosa,
    CategoriaInexistente,
    VinculacionTerminada
}
