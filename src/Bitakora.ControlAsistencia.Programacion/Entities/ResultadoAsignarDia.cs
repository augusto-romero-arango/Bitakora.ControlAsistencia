namespace Bitakora.ControlAsistencia.Programacion.Entities;

// SinCambios es exito silencioso (204 sin evento nuevo), no un rechazo -- mismo mecanismo que
// ResultadoAsignacionSede (Colaboradores).
internal enum ResultadoAsignarDia
{
    Asignado,
    SinCambios,
    SemanaFueraDeRango,

    // Issue #623: precedencia final -- PlantillaRetirada > SemanaFueraDeRango > SinCambios.
    PlantillaRetirada
}
