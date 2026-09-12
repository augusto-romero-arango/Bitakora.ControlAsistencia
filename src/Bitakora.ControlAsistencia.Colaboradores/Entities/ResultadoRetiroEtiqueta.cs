namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #663 (MEF-ADR-0004 "Estado ya alcanzado: no-op exitoso"): retirar una categoria sin
// etiqueta en la vinculacion vigente es un no-op exitoso -- SinCambios, gemelo de
// ResultadoAsignacionEtiqueta.SinCambios, no un rechazo (revierte la decision #2 de #355: el typo
// ya no aflora).
// VinculacionTerminada sigue siendo la unica razon de rechazo real (CA-ADR-0030), evaluable solo
// con la historia del stream: la ULTIMA vinculacion tiene terminacion registrada (incluye un
// preaviso sin vencer) -- las etiquetas describen la relacion laboral ACTIVA.
// internal: mismo criterio de visibilidad que los resultados hermanos.
internal enum ResultadoRetiroEtiqueta
{
    Exitosa,
    SinCambios,
    VinculacionTerminada
}
