using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction;

public partial class FunctionEndpoint
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction.FunctionEndpointMensajes",
        typeof(FunctionEndpoint).Assembly);

    internal static class Mensajes
    {
        public static string CodigoNoEsUrlSafe => ResourceManager.GetString(nameof(CodigoNoEsUrlSafe))!;
    }
}
