using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Evidencia auditable de una DepuracionDiaRecibida que llego DESPUES de aprobar el dia: espejo
// exacto de esa foto porque la evidencia vale por lo que la foto decia, no solo por haber llegado.
// Se persiste en el mismo stream que DiaAprobado y el aggregate no la incorpora -- Estado y valores
// decididos quedan intactos.
// MEF-ADR-0024: evento del aggregate, sin marker de bus -- consumidores: ninguno todavia.
public sealed class DepuracionPosAprobacionRecibida
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

    public DepuracionPosAprobacionRecibida(
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

    // El espejo se arma junto a la declaracion de campos: sumar un campo a la foto obliga a
    // sumarlo aqui, o la evidencia guardaria menos de lo que llego.
    public static DepuracionPosAprobacionRecibida Desde(DepuracionDiaRecibida foto) =>
        new(foto.Id, foto.CodigoColaborador, foto.Fecha, foto.Colaborador, foto.NombreTurno,
            foto.Franjas, foto.Marcaciones, foto.HorasDiscriminadas);

    // Constructor privado para Marten/serializacion
    private DepuracionPosAprobacionRecibida() { }

    // Configuracion de serializacion STJ/Marten: permite deserializar con constructor privado
    // y propiedades con private set. Ver MEF-ADR-0012 y DepuracionPosAprobacionRecibidaSerializacionTests.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(DepuracionPosAprobacionRecibida)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(DepuracionPosAprobacionRecibida)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (DepuracionPosAprobacionRecibida)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(DepuracionPosAprobacionRecibida).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
