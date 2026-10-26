using System.Text.Json.Serialization;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoCasillaMalla
{
    Trabajo,
    Descanso,
    Ausencia,
    SinProgramar,
    FueraDeVinculacion
}

public sealed record CasillaMalla(
    DateOnly Fecha,
    TipoCasillaMalla Tipo,
    string? NombreTurno,
    string? HorarioResumido,
    IReadOnlyList<Bloque> Bloques,
    string? MotivoAusencia,
    bool EsHorarioAbierto = false);

public sealed record FilaMallaSemanal(string CodigoColaborador, IReadOnlyList<CasillaMalla> Casillas);

public sealed record MallaSemanalTurnos(
    DateOnly Desde, DateOnly Hasta, int AnioIso, int NumeroSemana,
    IReadOnlyList<FilaMallaSemanal> Elementos, string? SiguienteCursor);
