namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class BorradorDeProgramacionDescartado
{
    public ColaboradorProgramado Colaborador { get; private set; } = null!;
    public IReadOnlyList<DateOnly> Fechas { get; private set; } = [];

    public BorradorDeProgramacionDescartado(ColaboradorProgramado colaborador, IReadOnlyList<DateOnly> fechas)
    {
        Colaborador = colaborador;
        Fechas = fechas;
    }

    private BorradorDeProgramacionDescartado() { }
}
