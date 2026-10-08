using System.Resources;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos;

// Textos de presentacion de las advertencias (MEF-ADR-0009): viven en .resx, no en la vista.
internal static class DescripcionesDeAdvertencias
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos.DescripcionesDeAdvertenciasMensajes",
        typeof(DescripcionesDeAdvertencias).Assembly);

    public static string Describir(string tipo, int? semana, int? dia, object magnitud)
    {
        var plantilla = ResourceManager.GetString(tipo) ?? tipo;
        var texto = magnitud switch
        {
            HorasYMinutosRespuesta hm => $"{hm.Horas}:{hm.Minutos:00}",
            _ => magnitud.ToString()
        };
        return string.Format(plantilla, semana, dia, texto);
    }
}
