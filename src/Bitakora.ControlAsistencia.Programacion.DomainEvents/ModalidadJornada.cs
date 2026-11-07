using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class ModalidadJornada
{
    public static readonly ModalidadJornada Ordinaria = new("Ordinaria");
    public static readonly ModalidadJornada Flexible = new("Flexible");
    public static readonly ModalidadJornada TurnosSucesivos = new("TurnosSucesivos");
    public static readonly ModalidadJornada SinSolucionDeContinuidad = new("SinSolucionDeContinuidad");
    public static readonly ModalidadJornada ExcluidaDeJornadaMaxima = new("ExcluidaDeJornadaMaxima");

    private static readonly IReadOnlyList<ModalidadJornada> Todas =
        [Ordinaria, Flexible, TurnosSucesivos, SinSolucionDeContinuidad, ExcluidaDeJornadaMaxima];

    private ModalidadJornada(string nombre) => Nombre = nombre;

    private ModalidadJornada() { }

    public string Nombre { get; private set; } = null!;

    public static ModalidadJornada Desde(string nombre) =>
        TryDesde(nombre, out var modalidad)
            ? modalidad!
            : throw new ArgumentException(Mensajes.NombreNoReconocido, nameof(nombre));

    public static bool TryDesde(string? nombre, out ModalidadJornada? modalidad)
    {
        modalidad = Todas.FirstOrDefault(m =>
            string.Equals(m.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
        return modalidad is not null;
    }

    public override bool Equals(object? obj) => obj is ModalidadJornada otra && otra.Nombre == Nombre;

    public override int GetHashCode() => Nombre.GetHashCode();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(ModalidadJornada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(ModalidadJornada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (ModalidadJornada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(ModalidadJornada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }

    public override string ToString() => ResourceManager.GetString(Nombre)!;
}
