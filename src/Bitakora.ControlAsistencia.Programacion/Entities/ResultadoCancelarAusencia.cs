using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

internal abstract record ResultadoCancelarAusencia
{
    internal sealed record Canceladas(
        ColaboradorProgramado Colaborador, IReadOnlyList<DateOnly> Fechas) : ResultadoCancelarAusencia;

    internal sealed record SinCambios : ResultadoCancelarAusencia;

    internal sealed record AusenciaInexistente : ResultadoCancelarAusencia;
}
