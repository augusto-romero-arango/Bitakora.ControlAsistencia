using Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador;

namespace Bitakora.ControlAsistencia.Colaboradores.ListarFichasColaborador;

public sealed record ListaFichasColaborador(IReadOnlyList<FichaColaboradorRespuesta> Elementos, string? SiguienteCursor);
