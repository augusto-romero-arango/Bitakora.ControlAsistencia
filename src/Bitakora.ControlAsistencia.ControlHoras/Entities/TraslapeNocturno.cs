namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Las marcaciones antes de la hora de corte pertenecen tambien al dia anterior (turnos nocturnos que
// cruzan medianoche). Constante de dominio, no del aggregate: cuando sea configurable por empresa
// vendra de un servicio externo.
internal static class TraslapeNocturno
{
    internal static readonly TimeOnly HoraCorte = new(4, 0);
}
