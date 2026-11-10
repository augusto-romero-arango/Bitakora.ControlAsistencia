using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// Un recurso por concepto, con clave Concepto.ToString() (el codigo estable: "OrdinariaDiurna", ...).
// El codigo nunca se traduce y sigue siendo la clave de MinutosPorConcepto; solo el valor del recurso
// es texto humano.
public sealed partial record IntervaloClasificado
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.ValueObjects.IntervaloClasificadoMensajes",
        typeof(IntervaloClasificado).Assembly);

    public static class Mensajes
    {
        public static string Etiqueta(Concepto concepto) =>
            ResourceManager.GetString(concepto.ToString())!;
    }
}
