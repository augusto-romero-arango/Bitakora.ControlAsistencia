using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;

public static class ComponedorMallaSemanal
{
    public static IReadOnlyList<FilaMallaSemanal> Componer(
        SemanaIso semana,
        IReadOnlyList<ColaboradorVigente> colaboradores,
        IEnumerable<TurnoVigente> documentos) => throw new NotImplementedException();
}
