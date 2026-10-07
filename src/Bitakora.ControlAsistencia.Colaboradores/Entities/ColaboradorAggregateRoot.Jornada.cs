using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

public partial class ColaboradorAggregateRoot
{
    private Guid? _jornadaId;

    internal Guid? JornadaId => _jornadaId;

    internal ResultadoAsignacionJornada AsignarJornada(Guid jornadaId) => throw new NotImplementedException();

    public void Apply(JornadaAsignada e) => throw new NotImplementedException();
}
