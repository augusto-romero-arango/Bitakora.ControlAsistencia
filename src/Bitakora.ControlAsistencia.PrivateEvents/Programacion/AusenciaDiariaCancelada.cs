using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

public sealed class AusenciaDiariaCancelada : IPrivateEvent
{
    public Guid AusenciaId { get; private set; }
    public ResumenColaborador Colaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }

    public AusenciaDiariaCancelada(Guid ausenciaId, ResumenColaborador colaborador, DateOnly fecha)
    {
        AusenciaId = ausenciaId;
        Colaborador = colaborador;
        Fecha = fecha;
    }

    private AusenciaDiariaCancelada() { }
}
