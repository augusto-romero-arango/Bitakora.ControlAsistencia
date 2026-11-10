namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Precedencia: TurnoRetirado > FranjaNoExiste > SubFranjaNoExiste.
internal enum ResultadoQuitarSubFranja
{
    Quitada,
    TurnoRetirado,
    TurnoDeHorarioAbierto,
    FranjaNoExiste,
    SubFranjaNoExiste
}
