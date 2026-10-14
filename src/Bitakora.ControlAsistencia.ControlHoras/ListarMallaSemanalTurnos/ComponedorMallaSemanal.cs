using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;

public static class ComponedorMallaSemanal
{
    public static IReadOnlyList<FilaMallaSemanal> Componer(
        SemanaIso semana,
        IReadOnlyList<ColaboradorVigente> colaboradores,
        IEnumerable<TurnoVigente> documentos)
    {
        var porClave = documentos
            .Where(d => d.Fecha >= semana.Lunes && d.Fecha <= semana.Domingo)
            .GroupBy(d => (d.CodigoColaborador, d.Fecha))
            .ToDictionary(g => g.Key, g => g.First());

        return colaboradores
            .OrderBy(c => c.CodigoColaborador, StringComparer.Ordinal)
            .Select(c => new FilaMallaSemanal(
                c.CodigoColaborador,
                Enumerable.Range(0, 7)
                    .Select(i => semana.Lunes.AddDays(i))
                    .Select(fecha => Casilla(
                        fecha, c, porClave.GetValueOrDefault((c.CodigoColaborador, fecha))))
                    .ToList()))
            .ToList();
    }

    private static CasillaMalla Casilla(DateOnly fecha, ColaboradorVigente colaborador, TurnoVigente? doc)
    {
        if (doc is null)
        {
            var tipoVacia = colaborador.EstaVinculadoEl(fecha)
                ? TipoCasillaMalla.SinProgramar
                : TipoCasillaMalla.FueraDeVinculacion;
            return new CasillaMalla(fecha, tipoVacia, null, null, [], null);
        }

        if (doc.MotivoAusencia is not null)
            return new CasillaMalla(fecha, TipoCasillaMalla.Ausencia, null, null, [], doc.MotivoAusencia);

        var tipo = doc.Bloques.Count == 0 ? TipoCasillaMalla.Descanso : TipoCasillaMalla.Trabajo;
        return new CasillaMalla(fecha, tipo, doc.NombreTurno, doc.HorarioResumido, doc.Bloques, null);
    }
}
