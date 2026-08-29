using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Issue #489: cierre del ciclo Provisional -> Aprobado. Unico evento del acto de aprobar -- las
// decisiones de sede viajan DENTRO de este evento (decision de sesion 2026-08-29: un evento
// separado insinuaria una resolucion con vida propia, ya rechazada al cerrar #483). Persistido en
// el mismo stream de DiaCalculadoAggregateRoot que DepuracionDiaRecibida; sin marker de bus --
// consumidores: ninguno todavia (MEF-ADR-0039, nace con el primer consumidor real).
public sealed class DiaAprobado
{
    // Id es el stream key de DiaCalculado, tal como lo computa DiaCalculadoAggregateRoot.ComputarStreamId.
    public string Id { get; private set; } = null!;
    public string CodigoColaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }

    // Vacia cuando el dia no tuvo franjas en conflicto: el acto aprueba el dia completo, las
    // decisiones de sede son solo el insumo que el acto exige donde la maquina se abstuvo.
    public IReadOnlyList<SedeDecidida> SedesDecididas { get; private set; } = null!;

    public DiaAprobado(
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

    // Constructor privado para Marten/serializacion
    private DiaAprobado() { }

    // Configuracion de serializacion STJ/Marten: permite deserializar con constructor privado
    // y propiedades con private set. Ver MEF-ADR-0012 y DiaAprobadoSerializacionTests.
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
