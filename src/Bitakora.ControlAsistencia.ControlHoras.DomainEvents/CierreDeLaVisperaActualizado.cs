using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public sealed class CierreDeLaVisperaActualizado
{
    public string Id { get; private set; } = null!;
    public DateTime? Cierre { get; private set; }

    private CierreDeLaVisperaActualizado() { }

    public static CierreDeLaVisperaActualizado Crear(string id, DateTime? cierre) =>
        throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
