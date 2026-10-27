using System.Resources;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerCuadroDeTurnos;

internal static class DescripcionesDeAdvertenciasDeCuadroDeTurnos
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.ObtenerCuadroDeTurnos.DescripcionesDeAdvertenciasDeCuadroDeTurnosMensajes",
        typeof(DescripcionesDeAdvertenciasDeCuadroDeTurnos).Assembly);

    public static string Describir(string tipo, int? dia, HorasYMinutosRespuesta magnitud)
    {
        var plantilla = ResourceManager.GetString(tipo) ?? tipo;
        return string.Format(plantilla, dia, $"{magnitud.Horas}:{magnitud.Minutos:00}");
    }
}
