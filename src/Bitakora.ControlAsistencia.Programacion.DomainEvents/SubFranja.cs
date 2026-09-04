using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Segmento temporal dentro de una franja ordinaria.
// Puede representar un descanso o una hora extra segun la lista que lo contenga.
// Es hoja - no contiene otras franjas.
// ADR-0015: sealed class con factory static, constructor privado, campos readonly.
public sealed class SubFranja : FranjaTemporal, IEquatable<SubFranja>
{
    // Constructor real: usado por el factory
    private SubFranja(TimeOnly horaInicio, TimeOnly horaFin,
        int diaOffsetInicio, int diaOffsetFin)
        : base(horaInicio, horaFin, diaOffsetInicio, diaOffsetFin) { }

    // Constructor vacio para STJ/Marten
    private SubFranja() { }

    // CA-8: factory estatico
    // Issue #598 CA-1, CA-2: rechaza duracion no positiva (generaliza el caso inicio == fin)
    public static SubFranja Crear(TimeOnly horaInicio, TimeOnly horaFin,
        int diaOffsetInicio = 0, int diaOffsetFin = 0)
    {
        var franja = new SubFranja(horaInicio, horaFin, diaOffsetInicio, diaOffsetFin);

        if (franja.DuracionEnMinutos() <= 0)
            throw new ArgumentException(Mensajes.DuracionNoPositiva);

        return franja;
    }

    // Conversion al DTO plano propio del dominio (Programacion.DomainEvents.SubFranjaProgramada).
    // Issue #319 (tres islas): ya no retorna el DTO de bus (DetalleSubFranja, PrivateEvents) -- el
    // FA mapea SubFranjaProgramada -> DetalleSubFranja solo para los eventos que cruzan el bus
    // (CA-5). Tell-don't-Ask preservado: la conversion sigue viviendo en este VO (MEF-ADR-0012).
    public SubFranjaProgramada ToDetalle() =>
        new(_horaInicio, _horaFin, _diaOffsetInicio, _diaOffsetFin, ToString());

    // CA-20, CA-21: formato legible con offsets
    public override string ToString() =>
        $"({FormatearHora(_horaInicio, _diaOffsetInicio)}-{FormatearHora(_horaFin, _diaOffsetFin)})";

    // Igualdad por valor
    public bool Equals(SubFranja? other) =>
        other is not null
        && _horaInicio == other._horaInicio
        && _horaFin == other._horaFin
        && _diaOffsetInicio == other._diaOffsetInicio
        && _diaOffsetFin == other._diaOffsetFin;

    public override bool Equals(object? obj) => Equals(obj as SubFranja);

    public override int GetHashCode() =>
        HashCode.Combine(_horaInicio, _horaFin, _diaOffsetInicio, _diaOffsetFin);

    // Mapping de serializacion - vive aqui porque cambia con la clase
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(SubFranja)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(SubFranja)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (SubFranja)ctor.Invoke(null);

            RegistrarCampo(typeInfo, "_horaInicio", "horaInicio", typeof(TimeOnly), typeof(FranjaTemporal));
            RegistrarCampo(typeInfo, "_horaFin", "horaFin", typeof(TimeOnly), typeof(FranjaTemporal));
            RegistrarCampo(typeInfo, "_diaOffsetInicio", "diaOffsetInicio", typeof(int), typeof(FranjaTemporal));
            RegistrarCampo(typeInfo, "_diaOffsetFin", "diaOffsetFin", typeof(int), typeof(FranjaTemporal));
        });
    }
}
