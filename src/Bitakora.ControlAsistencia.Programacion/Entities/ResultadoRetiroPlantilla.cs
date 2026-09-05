namespace Bitakora.ControlAsistencia.Programacion.Entities;

// A diferencia de ResultadoRetiroTurno, no tiene YaEstabaRetirado: retirar dos veces es exito
// silencioso (204 sin evento nuevo), no rechazo.
internal enum ResultadoRetiroPlantilla
{
    Retirada,
    SinCambios
}
