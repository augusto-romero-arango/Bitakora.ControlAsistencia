namespace Bitakora.ControlAsistencia.Programacion.Entities;

// SinCambios es exito silencioso (204 sin evento nuevo), no un rechazo.
internal enum ResultadoAsignarDia
{
    Asignado,
    SinCambios,
    SemanaFueraDeRango,
    PlantillaRetirada
}
