using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.ResolverAmbiguedadDeMarcacionesFunction;

public sealed record ResolverAmbiguedadDeMarcaciones(
    string CodigoColaborador,
    DateOnly Fecha,
    IReadOnlyList<DecisionDeMarcacion> Marcaciones);
