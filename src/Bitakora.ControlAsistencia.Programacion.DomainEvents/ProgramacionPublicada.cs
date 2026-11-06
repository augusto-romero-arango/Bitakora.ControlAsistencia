namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class ProgramacionPublicada
{
    public ColaboradorProgramado Colaborador { get; private set; } = null!;
    public IReadOnlyList<DateOnly> Fechas { get; private set; } = [];

    public ProgramacionPublicada(ColaboradorProgramado colaborador, IReadOnlyList<DateOnly> fechas)
    {
        Colaborador = colaborador;
        Fechas = fechas;
    }

    private ProgramacionPublicada() { }
}
