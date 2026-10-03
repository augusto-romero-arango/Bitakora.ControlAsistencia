using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class MotivoAusencia
{
    public static readonly MotivoAusencia Vacaciones = new("Vacaciones");
    public static readonly MotivoAusencia IncapacidadMedica = new("IncapacidadMedica");
    public static readonly MotivoAusencia LicenciaRemunerada = new("LicenciaRemunerada");
    public static readonly MotivoAusencia AusenciaNoRemunerada = new("AusenciaNoRemunerada");

    private MotivoAusencia(string nombre) => Nombre = nombre;

    public string Nombre { get; }

    public static MotivoAusencia Desde(string nombre) => throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
    }

    public override string ToString() => Nombre;
}
