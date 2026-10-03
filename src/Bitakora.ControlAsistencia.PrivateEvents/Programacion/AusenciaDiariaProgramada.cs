using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

public sealed class AusenciaDiariaProgramada : IPrivateEvent
{
    public Guid AusenciaId { get; private set; }
    public ResumenColaborador Colaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public string Motivo { get; private set; } = null!;

    public AusenciaDiariaProgramada(
        Guid ausenciaId, ResumenColaborador colaborador, DateOnly fecha, string motivo)
    {
        AusenciaId = ausenciaId;
        Colaborador = colaborador;
        Fecha = fecha;
        Motivo = motivo;
    }

    private AusenciaDiariaProgramada() { }
}
