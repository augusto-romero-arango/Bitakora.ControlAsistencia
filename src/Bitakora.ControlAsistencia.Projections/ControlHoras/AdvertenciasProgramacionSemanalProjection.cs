using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Projections; // MultiStreamProjection<,> vive aqui

namespace Bitakora.ControlAsistencia.Projections.ControlHoras;

/// <summary>Proyeccion companion N2 de AdvertenciasProgramacionSemanal (stub de fase roja).</summary>
public sealed partial class AdvertenciasProgramacionSemanalProjection
    : MultiStreamProjection<AdvertenciasProgramacionSemanal, string>
{
    public AdvertenciasProgramacionSemanalProjection()
    {
        Identity<TurnoDiarioAsignado>(e => Clave(e.InformacionColaborador.CodigoColaborador, e.Fecha));
        Identity<TurnoDiarioCancelado>(e => Clave(e.Colaborador.CodigoColaborador, e.Fecha));
        Identity<AusenciaDiariaAsignada>(e => Clave(e.Colaborador.CodigoColaborador, e.Fecha));
        Identity<CancelacionAusenciaDiariaRegistrada>(e => ClaveDesdeStream(e.Id, e.Fecha));
    }

    public static string Clave(string codigoColaborador, DateOnly fecha) =>
        throw new NotImplementedException();

    public static string ClaveDesdeStream(string streamId, DateOnly fecha) =>
        throw new NotImplementedException();

    public static AdvertenciasProgramacionSemanal Create(TurnoDiarioAsignado evento) =>
        throw new NotImplementedException();

    public static AdvertenciasProgramacionSemanal Apply(TurnoDiarioAsignado evento, AdvertenciasProgramacionSemanal vista) =>
        throw new NotImplementedException();

    public static AdvertenciasProgramacionSemanal Create(AusenciaDiariaAsignada evento) =>
        throw new NotImplementedException();

    public static AdvertenciasProgramacionSemanal Apply(AusenciaDiariaAsignada evento, AdvertenciasProgramacionSemanal vista) =>
        throw new NotImplementedException();

    public static AdvertenciasProgramacionSemanal? Create(TurnoDiarioCancelado evento) =>
        throw new NotImplementedException();

    public static AdvertenciasProgramacionSemanal? Apply(TurnoDiarioCancelado evento, AdvertenciasProgramacionSemanal vista) =>
        throw new NotImplementedException();

    public static AdvertenciasProgramacionSemanal? Create(CancelacionAusenciaDiariaRegistrada evento) =>
        throw new NotImplementedException();

    public static AdvertenciasProgramacionSemanal? Apply(CancelacionAusenciaDiariaRegistrada evento, AdvertenciasProgramacionSemanal vista) =>
        throw new NotImplementedException();
}
