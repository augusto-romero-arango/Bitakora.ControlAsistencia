using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Marten.Events.Projections;

namespace Bitakora.ControlAsistencia.Projections.Programacion;

public sealed partial class BorradorDeProgramacionProjection : MultiStreamProjection<BorradorDeProgramacion, string>
{
    public BorradorDeProgramacionProjection()
    {
        Identity<ProgramacionTurnoAgregadaAlBorrador>(e => e.Colaborador.CodigoColaborador);
        Identity<ProgramacionPublicada>(e => e.Colaborador.CodigoColaborador);
        Identity<BorradorDeProgramacionDescartado>(e => e.Colaborador.CodigoColaborador);
    }

    public static BorradorDeProgramacion Create(ProgramacionTurnoAgregadaAlBorrador e) =>
        throw new NotImplementedException();

    public static BorradorDeProgramacion Apply(ProgramacionTurnoAgregadaAlBorrador e, BorradorDeProgramacion vista) =>
        throw new NotImplementedException();

    public static BorradorDeProgramacion? Apply(ProgramacionPublicada e, BorradorDeProgramacion vista) =>
        throw new NotImplementedException();

    public static BorradorDeProgramacion? Apply(BorradorDeProgramacionDescartado e, BorradorDeProgramacion vista) =>
        throw new NotImplementedException();
}
