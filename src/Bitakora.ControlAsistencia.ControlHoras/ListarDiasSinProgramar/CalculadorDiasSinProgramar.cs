namespace Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

public static class CalculadorDiasSinProgramar
{
    /// <summary>Dias de la ventana, dentro de la vigencia de cada colaborador, sin documento TurnoVigente. Solo colaboradores con faltantes, ordenados por codigo.</summary>
    public static IReadOnlyList<ElementoDiasSinProgramar> Calcular(
        DateOnly desde, DateOnly hasta,
        IReadOnlyList<ColaboradorVigente> colaboradores,
        IEnumerable<(string CodigoColaborador, DateOnly Fecha)> diasProgramados) => throw new NotImplementedException();
}
