using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

// Horas extras programadas dentro de una franja ordinaria.
// Representan un compromiso del empleado.
// Es hoja - no contiene otras franjas.
// ADR-0015: sealed class con factory static, constructor privado, campos readonly.
public sealed class FranjaExtra : FranjaTemporal, IEquatable<FranjaExtra>
{
    // Constructor real: usado por el factory
    private FranjaExtra(TimeOnly horaInicio, TimeOnly horaFin,
        int diaOffsetInicio, int diaOffsetFin)
        : base(horaInicio, horaFin, diaOffsetInicio, diaOffsetFin) { }

    // Constructor vacio para STJ/Marten
    private FranjaExtra() { }

    // CA-8: factory estatico
    // CA-7: rechaza InicioYFinIguales
    public static FranjaExtra Crear(TimeOnly horaInicio, TimeOnly horaFin,
        int diaOffsetInicio = 0, int diaOffsetFin = 0)
    {
        if (horaInicio == horaFin && diaOffsetInicio == diaOffsetFin)
            throw new ArgumentException(Mensajes.InicioYFinIguales);

        return new FranjaExtra(horaInicio, horaFin, diaOffsetInicio, diaOffsetFin);
    }

    // CA-20, CA-21: formato legible con offsets
    public override string ToString() =>
        $"({FormatearHora(_horaInicio, _diaOffsetInicio)}-{FormatearHora(_horaFin, _diaOffsetFin)})";

    // Igualdad por valor
    public bool Equals(FranjaExtra? other) =>
        other is not null
        && _horaInicio == other._horaInicio
        && _horaFin == other._horaFin
        && _diaOffsetInicio == other._diaOffsetInicio
        && _diaOffsetFin == other._diaOffsetFin;

    public override bool Equals(object? obj) => Equals(obj as FranjaExtra);

    public override int GetHashCode() =>
        HashCode.Combine(_horaInicio, _horaFin, _diaOffsetInicio, _diaOffsetFin);

    // Mapping de serializacion - vive aqui porque cambia con la clase
    internal static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(FranjaExtra)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(FranjaExtra)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (FranjaExtra)ctor.Invoke(null);

            RegistrarCampo(typeInfo, "_horaInicio", "horaInicio", typeof(TimeOnly), typeof(FranjaTemporal));
            RegistrarCampo(typeInfo, "_horaFin", "horaFin", typeof(TimeOnly), typeof(FranjaTemporal));
            RegistrarCampo(typeInfo, "_diaOffsetInicio", "diaOffsetInicio", typeof(int), typeof(FranjaTemporal));
            RegistrarCampo(typeInfo, "_diaOffsetFin", "diaOffsetFin", typeof(int), typeof(FranjaTemporal));
        });
    }
}
