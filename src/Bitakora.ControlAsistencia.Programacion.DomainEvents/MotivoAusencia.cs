using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class MotivoAusencia
{
    public static readonly MotivoAusencia Vacaciones = new("Vacaciones");
    public static readonly MotivoAusencia IncapacidadMedica = new("IncapacidadMedica");
    public static readonly MotivoAusencia LicenciaRemunerada = new("LicenciaRemunerada");
    public static readonly MotivoAusencia AusenciaNoRemunerada = new("AusenciaNoRemunerada");

    private static readonly IReadOnlyList<MotivoAusencia> Todos =
        [Vacaciones, IncapacidadMedica, LicenciaRemunerada, AusenciaNoRemunerada];

    private MotivoAusencia(string nombre) => Nombre = nombre;

    private MotivoAusencia() { }

    public string Nombre { get; private set; } = null!;

    public static MotivoAusencia Desde(string nombre) =>
        TryDesde(nombre, out var motivo)
            ? motivo!
            : throw new ArgumentException(Mensajes.NombreNoReconocido, nameof(nombre));

    public static bool TryDesde(string? nombre, out MotivoAusencia? motivo)
    {
        motivo = Todos.FirstOrDefault(m =>
            string.Equals(m.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
        return motivo is not null;
    }

    public override bool Equals(object? obj) => obj is MotivoAusencia otro && otro.Nombre == Nombre;

    public override int GetHashCode() => Nombre.GetHashCode();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(MotivoAusencia)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(MotivoAusencia)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (MotivoAusencia)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(MotivoAusencia).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }

    public override string ToString() => Nombre;
}
