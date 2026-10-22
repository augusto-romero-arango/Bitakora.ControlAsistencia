namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public record CoberturaDeMarcacion(DateTime Inicio, DateTime Fin)
{
    public bool Contiene(DateTime instante) => throw new NotImplementedException();
}
