namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Declina con resultado, nunca lanza (CA-ADR-0030). SinCambios es exito silencioso (204 sin
// evento nuevo) cuando la plantilla ya estaba retirada -- mismo mecanismo que ResultadoRetiroTurno,
// pero sin YaEstabaRetirado: aqui la idempotencia es exito, no rechazo (harness#850).
internal enum ResultadoRetiroPlantilla
{
    Retirada,
    SinCambios
}
