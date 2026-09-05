using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #604: se quita una franja ordinaria de un turno del catalogo -- corregir el diseno de
// turno por pasos es "quitar + agregar" (Rule of Three, MEF-ADR-0018: sin comando Mover*). El
// evento conserva la franja COMPLETA que se fue (con sus descansos, extras y sede): memoria
// auditable del stream y materia prima para el eco de la tool MCP (#609). No cruza ningun bus:
// solo se persiste en el event store de Programacion.
public sealed class FranjaQuitada
{
    public Guid TurnoId { get; private set; }
    public FranjaOrdinaria Franja { get; private set; } = null!;

    private FranjaQuitada(Guid turnoId, FranjaOrdinaria franja)
    {
        TurnoId = turnoId;
        Franja = franja;
    }

    // Constructor vacio privado para Marten/JSON (mismo patron que FranjaAgregada).
    private FranjaQuitada() { }

    public static FranjaQuitada Crear(Guid turnoId, FranjaOrdinaria franja) => new(turnoId, franja);

    // Mapping de serializacion para STJ/Marten -- mismo patron que FranjaAgregada.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(FranjaQuitada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(FranjaQuitada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (FranjaQuitada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(FranjaQuitada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
