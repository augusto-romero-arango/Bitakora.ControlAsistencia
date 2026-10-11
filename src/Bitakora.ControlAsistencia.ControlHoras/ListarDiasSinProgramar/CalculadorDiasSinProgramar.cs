namespace Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

public static class CalculadorDiasSinProgramar
{
    /// <summary>Dias de la ventana, dentro de la vigencia de cada colaborador, sin documento TurnoVigente. Solo colaboradores con faltantes, ordenados por codigo.</summary>
    public static IReadOnlyList<ElementoDiasSinProgramar> Calcular(
        DateOnly desde, DateOnly hasta,
        IReadOnlyList<ColaboradorVigente> colaboradores,
        IEnumerable<(string CodigoColaborador, DateOnly Fecha)> diasProgramados)
    {
        var programados = diasProgramados.ToHashSet();
        var resultado = new List<ElementoDiasSinProgramar>();

        foreach (var colaborador in colaboradores.OrderBy(c => c.CodigoColaborador, StringComparer.Ordinal))
        {
            var inicio = colaborador.VigenteDesde > desde ? colaborador.VigenteDesde : desde;
            var fin = colaborador.VigenteHasta is { } vh && vh < hasta ? vh : hasta;

            var faltantes = new List<DateOnly>();
            for (var dia = inicio; dia <= fin; dia = dia.AddDays(1))
                if (!programados.Contains((colaborador.CodigoColaborador, dia)))
                    faltantes.Add(dia);

            if (faltantes.Count > 0)
                resultado.Add(new ElementoDiasSinProgramar(colaborador.CodigoColaborador, faltantes));
        }

        return resultado;
    }
}
