using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

public partial class ColaboradorAggregateRoot
{
    private Guid? _jornadaId;

    internal Guid? JornadaId => _jornadaId;

    internal ResultadoAsignacionJornada AsignarJornada(Guid jornadaId)
    {
        if (_fechaTerminacionVinculacionVigente is not null)
            return ResultadoAsignacionJornada.VinculacionTerminada;

        if (_jornadaId == jornadaId)
            return ResultadoAsignacionJornada.SinCambios;

        var evento = new JornadaAsignada(jornadaId);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoAsignacionJornada.Exitosa;
    }

    public void Apply(JornadaAsignada e) => _jornadaId = e.JornadaId;
}
