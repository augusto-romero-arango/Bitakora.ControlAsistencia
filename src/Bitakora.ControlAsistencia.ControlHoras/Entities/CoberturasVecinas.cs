namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

public record CoberturasVecinas(DateTime? FinDeLaVispera, DateTime? InicioDelDiaSiguiente)
{
    public bool Excluye(DateTime instante) => throw new NotImplementedException();
}
