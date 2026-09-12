namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Mismo criterio que ResultadoRetiroTurno.SinCambios (harness#850): retirar dos veces es exito
// silencioso (204 sin evento nuevo), no rechazo.
internal enum ResultadoRetiroPlantilla
{
    Retirada,
    SinCambios
}
