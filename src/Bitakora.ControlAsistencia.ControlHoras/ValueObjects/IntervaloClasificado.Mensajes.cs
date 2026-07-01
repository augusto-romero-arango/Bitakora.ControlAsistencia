using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// Issue #184: la trazabilidad (memoria de calculo) que viaja en HorasDiscriminadas es texto humano
// traducido en el back (.resx). La etiqueta legible de cada Concepto vive aqui porque IntervaloClasificado
// es el tipo que empareja intervalo + concepto y es el que se renderiza para nomina via ToString().
// ADR de mensajes .resx (i18n de los ToString()): el patron es ResourceManager + accesor estatico, igual
// que IntervaloTemporal/Retardo. Variacion menor sobre el patron: en vez de una propiedad por clave,
// un metodo Etiqueta(Concepto) que resuelve la clave desde Concepto.ToString() (hay un recurso por concepto).
//
// CA-4: la clave del recurso ES Concepto.ToString() (el codigo estable: "OrdinariaDiurna", ...). El codigo
// nunca se traduce y sigue siendo la clave de MinutosPorConcepto; solo el VALOR del recurso es texto humano.
public sealed partial record IntervaloClasificado
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.ValueObjects.IntervaloClasificadoMensajes",
        typeof(IntervaloClasificado).Assembly);

    public static class Mensajes
    {
        // Texto humano traducido de un Concepto, para la trazabilidad. La clave es Concepto.ToString().
        public static string Etiqueta(Concepto concepto) =>
            ResourceManager.GetString(concepto.ToString())!;
    }
}
