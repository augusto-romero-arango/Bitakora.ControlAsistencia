using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class BorradorMensual : AggregateRoot
{
    internal IReadOnlyList<DateOnly> FechasEnBorrador { get; private set; } = [];

    public static string ComputarStreamId(string codigoColaborador, DateOnly fecha)
        => throw new NotImplementedException();

    public void Apply(ProgramacionTurnoAgregadaAlBorrador e) => throw new NotImplementedException();

    public void Apply(ProgramacionPublicada e) => throw new NotImplementedException();

    internal ResultadoPublicacion Publicar(DateOnly desde, DateOnly hasta, AusenciasColaborador? ausencias)
        => throw new NotImplementedException();
}
