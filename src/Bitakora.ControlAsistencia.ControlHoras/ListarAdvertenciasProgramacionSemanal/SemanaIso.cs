namespace Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

public sealed record SemanaIso(int Anio, int Numero, DateOnly Lunes, DateOnly Domingo)
{
    public static SemanaIso De(DateOnly fecha) => throw new NotImplementedException();
}
