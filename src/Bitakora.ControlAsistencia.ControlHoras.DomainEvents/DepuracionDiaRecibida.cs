using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Foto completa de un dia depurado, persistida en el stream de DiaCalculadoAggregateRoot. Cada
// recepcion emite una: no hay deduplicacion de ningun tipo (el productor controla los duplicados
// de negocio y re-aplicar la misma foto es idempotente).
// MEF-ADR-0024: evento del aggregate, sin marker de bus.
public sealed class DepuracionDiaRecibida
{
    // Id es el stream key de DiaCalculado, tal como lo computa DiaCalculadoAggregateRoot.ComputarStreamId.
    public string Id { get; private set; } = null!;
    public string CodigoColaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public ResumenColaborador? Colaborador { get; private set; }
    public string? NombreTurno { get; private set; }
    public IReadOnlyList<FranjaDepurada> Franjas { get; private set; } = null!;
    public IReadOnlyList<MarcacionDelDia> Marcaciones { get; private set; } = null!;
    public HorasDiscriminadas HorasDiscriminadas { get; private set; } = null!;

    public DepuracionDiaRecibida(
        string id,
        string codigoColaborador,
        DateOnly fecha,
        ResumenColaborador? colaborador,
        string? nombreTurno,
        IReadOnlyList<FranjaDepurada> franjas,
        IReadOnlyList<MarcacionDelDia> marcaciones,
        HorasDiscriminadas horasDiscriminadas)
    {
        Id = id;
        CodigoColaborador = codigoColaborador;
        Fecha = fecha;
        Colaborador = colaborador;
        NombreTurno = nombreTurno;
        Franjas = franjas;
        Marcaciones = marcaciones;
        HorasDiscriminadas = horasDiscriminadas;
    }

    // Constructor privado para Marten/serializacion
    private DepuracionDiaRecibida() { }

    // Configuracion de serializacion STJ/Marten: permite deserializar con constructor privado
    // y propiedades con private set. Ver MEF-ADR-0012 y DepuracionDiaRecibidaSerializacionTests.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(DepuracionDiaRecibida)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(DepuracionDiaRecibida)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (DepuracionDiaRecibida)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(DepuracionDiaRecibida).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
