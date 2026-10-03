using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public sealed class AusenciaDiariaAsignada
{
    public string Id { get; private set; } = null!;
    public ColaboradorProgramado Colaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public Guid AusenciaId { get; private set; }
    public string Motivo { get; private set; } = null!;

    private AusenciaDiariaAsignada(
        string id, ColaboradorProgramado colaborador, DateOnly fecha, Guid ausenciaId, string motivo)
    {
        Id = id;
        Colaborador = colaborador;
        Fecha = fecha;
        AusenciaId = ausenciaId;
        Motivo = motivo;
    }

    private AusenciaDiariaAsignada() { }

    public static AusenciaDiariaAsignada Crear(
        string id, ColaboradorProgramado colaborador, DateOnly fecha, Guid ausenciaId, string motivo) =>
        new(id, colaborador, fecha, ausenciaId, motivo);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(AusenciaDiariaAsignada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(AusenciaDiariaAsignada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (AusenciaDiariaAsignada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(AusenciaDiariaAsignada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
