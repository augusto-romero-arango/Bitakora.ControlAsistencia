using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

// Pausa operativa dentro de una franja ordinaria (ej. almuerzo).
// Es hoja - no contiene otras franjas.
// ADR-0015: sealed class con factory static, constructor privado, campos readonly.
public sealed class FranjaDescanso : FranjaTemporal, IEquatable<FranjaDescanso>
{
    // CA-5: offsets respecto al dia de la ordinaria padre
    private readonly int _diaOffsetInicio;
    private readonly int _diaOffsetFin;

    // Constructor real: usado por el factory
    private FranjaDescanso(TimeOnly horaInicio, TimeOnly horaFin,
        int diaOffsetInicio, int diaOffsetFin)
        : base(horaInicio, horaFin)
    {
        _diaOffsetInicio = diaOffsetInicio;
        _diaOffsetFin = diaOffsetFin;
    }

    // Constructor vacio para STJ/Marten
    private FranjaDescanso() { }

    internal override int MinutosAbsolutoInicio => CalcularMinutosAbsolutos(_horaInicio, _diaOffsetInicio);
    internal override int MinutosAbsolutoFin => CalcularMinutosAbsolutos(_horaFin, _diaOffsetFin);

    // CA-8: factory estatico
    // CA-7: rechaza InicioYFinIguales
    public static FranjaDescanso Crear(TimeOnly horaInicio, TimeOnly horaFin,
        int diaOffsetInicio = 0, int diaOffsetFin = 0)
    {
        if (horaInicio == horaFin && diaOffsetInicio == diaOffsetFin)
            throw new ArgumentException(Mensajes.InicioYFinIguales);

        return new FranjaDescanso(horaInicio, horaFin, diaOffsetInicio, diaOffsetFin);
    }

    // CA-20, CA-21: formato legible con offsets
    public override string ToString() =>
        $"({FormatearHora(_horaInicio, _diaOffsetInicio)}-{FormatearHora(_horaFin, _diaOffsetFin)})";

    // Igualdad por valor
    public bool Equals(FranjaDescanso? other) =>
        other is not null
        && _horaInicio == other._horaInicio
        && _horaFin == other._horaFin
        && _diaOffsetInicio == other._diaOffsetInicio
        && _diaOffsetFin == other._diaOffsetFin;

    public override bool Equals(object? obj) => Equals(obj as FranjaDescanso);

    public override int GetHashCode() =>
        HashCode.Combine(_horaInicio, _horaFin, _diaOffsetInicio, _diaOffsetFin);

    // Mapping de serializacion - vive aqui porque cambia con la clase
    internal static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(FranjaDescanso)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(FranjaDescanso)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (FranjaDescanso)ctor.Invoke(null);

            RegistrarCampo(typeInfo, "_horaInicio", "horaInicio", typeof(TimeOnly), typeof(FranjaDescanso));
            RegistrarCampo(typeInfo, "_horaFin", "horaFin", typeof(TimeOnly), typeof(FranjaDescanso));
            RegistrarCampo(typeInfo, "_diaOffsetInicio", "diaOffsetInicio", typeof(int), typeof(FranjaDescanso));
            RegistrarCampo(typeInfo, "_diaOffsetFin", "diaOffsetFin", typeof(int), typeof(FranjaDescanso));
        });
    }
}
