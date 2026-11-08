using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerJornada;

internal static class DescripcionesDeAdvertenciasDeJornada
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.ObtenerJornada.DescripcionesDeAdvertenciasDeJornadaMensajes",
        typeof(DescripcionesDeAdvertenciasDeJornada).Assembly);

    public static IReadOnlyList<AdvertenciaJornadaRespuesta> Describir(ModalidadJornada? modalidad, LimitesJornada limites) =>
        modalidad is null ? [] : modalidad.Advertir(limites)
            .Select(a => Describir(a, modalidad, limites))
            .ToArray();

    private static AdvertenciaJornadaRespuesta Describir(
        AdvertenciaJornada advertencia, ModalidadJornada modalidad, LimitesJornada limites)
    {
        var actual = advertencia.Tipo switch
        {
            TipoAdvertenciaJornada.SuperaTopeDiarioDeLaModalidad => Formato(limites.TopeDiario.TotalMinutos()),
            TipoAdvertenciaJornada.SuperaHorasSemanalesDeLaModalidad => Formato(limites.HorasSemanales.TotalMinutos()),
            _ => Formato(limites.MinimoDiario.TotalMinutos())
        };
        var tipo = advertencia.Tipo.ToString();
        var descripcion = string.Format(ResourceManager.GetString(tipo)!,
            actual, Formato(advertencia.Magnitud), Formato(advertencia.ValorDeLey), modalidad);
        return new(tipo, Convertir(advertencia.Magnitud), Convertir(advertencia.ValorDeLey), descripcion);
    }

    private static string Formato(int minutos) => $"{minutos / 60}:{minutos % 60:00}";

    private static HorasYMinutosRespuesta Convertir(int minutos) => new(minutos / 60, minutos % 60);
}
