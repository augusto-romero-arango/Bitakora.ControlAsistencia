using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Cierre del ciclo Provisional -> Aprobado. Unico evento del acto de aprobar: las decisiones de
// sede viajan DENTRO de el, porque un evento separado insinuaria una resolucion con vida propia.
public sealed class DiaAprobado
{
    // Id es el stream key de DiaCalculado, tal como lo computa DiaCalculadoAggregateRoot.ComputarStreamId.
    public string Id { get; private set; } = null!;
    public string CodigoColaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }

    // Vacia cuando el dia no tuvo franjas en conflicto: el acto aprueba el dia completo, las
    // decisiones de sede son solo el insumo que el acto exige donde la maquina se abstuvo.
    public IReadOnlyList<SedeDecidida> SedesDecididas { get; private set; } = null!;

    // Privado a proposito: con un ctor publico parametrizado STJ lo resolveria solo y
    // ConfigurarSerializacion quedaria muerto (el guardrail "sin registro falla" pasaria en falso).
    private DiaAprobado(
        string id,
        string codigoColaborador,
        DateOnly fecha,
        IReadOnlyList<SedeDecidida> sedesDecididas)
    {
        Id = id;
        CodigoColaborador = codigoColaborador;
        Fecha = fecha;
        SedesDecididas = sedesDecididas;
    }

    // Solo para Marten/STJ via ConfigurarSerializacion.
    private DiaAprobado() { }

    public static DiaAprobado Crear(
        string id,
        string codigoColaborador,
        DateOnly fecha,
        IReadOnlyList<SedeDecidida> sedesDecididas) =>
        new(id, codigoColaborador, fecha, sedesDecididas);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(DiaAprobado)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(DiaAprobado)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (DiaAprobado)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(DiaAprobado).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
