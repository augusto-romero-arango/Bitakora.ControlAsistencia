namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// SinCambios es el no-op exitoso de MEF-ADR-0004 ("Estado ya alcanzado"), no un rechazo: la
// categoria no esta en la vinculacion vigente y no hay hecho nuevo que registrar.
// VinculacionTerminada es la unica razon de rechazo real (CA-ADR-0030): la ULTIMA vinculacion
// tiene terminacion registrada -- un preaviso SIN vencer bloquea igual, las etiquetas describen la
// relacion laboral ACTIVA.
// internal: mismo criterio de visibilidad que los resultados hermanos.
internal enum ResultadoRetiroEtiqueta
{
    Exitosa,
    SinCambios,
    VinculacionTerminada
}
