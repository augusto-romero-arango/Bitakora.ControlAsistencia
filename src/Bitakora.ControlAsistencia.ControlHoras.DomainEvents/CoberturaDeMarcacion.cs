namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public record CoberturaDeMarcacion(DateTime Inicio, DateTime Fin)
{
    public bool Contiene(DateTime instante) => instante >= Inicio && instante < Fin;
}
