namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// El nombre de cada miembro es el string que viaja en DiaDepurado y queda persistido en
// DepuracionDiaRecibida: renombrarlo rompe la lectura de lo ya escrito.
public enum PertenenciaMarcacion
{
    DelTurno,
    FueraDeCobertura,
    Ambigua
}
