namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public sealed class AusenciaDiariaAsignada
{
    public string Id { get; private set; } = null!;
    public ColaboradorProgramado Colaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public Guid AusenciaId { get; private set; }
    public string Motivo { get; private set; } = null!;

    private AusenciaDiariaAsignada(
        string id, ColaboradorProgramado colaborador, DateOnly fecha, Guid ausenciaId, string motivo)
    {
        Id = id;
        Colaborador = colaborador;
        Fecha = fecha;
        AusenciaId = ausenciaId;
        Motivo = motivo;
    }

    private AusenciaDiariaAsignada() { }

    public static AusenciaDiariaAsignada Crear(
        string id, ColaboradorProgramado colaborador, DateOnly fecha, Guid ausenciaId, string motivo) =>
        new(id, colaborador, fecha, ausenciaId, motivo);
}
