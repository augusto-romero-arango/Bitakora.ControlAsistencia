using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Descanso o extra segun la lista de la franja ordinaria que la contenga; es hoja.
public sealed class SubFranja : FranjaTemporal, IEquatable<SubFranja>
{
    private SubFranja(TimeOnly horaInicio, TimeOnly horaFin,
        int diaOffsetInicio, int diaOffsetFin)
        : base(horaInicio, horaFin, diaOffsetInicio, diaOffsetFin) { }

    // Constructor vacio para STJ/Marten
    private SubFranja() { }

    public static SubFranja Crear(TimeOnly horaInicio, TimeOnly horaFin,
        int diaOffsetInicio = 0, int diaOffsetFin = 0)
    {
        var franja = new SubFranja(horaInicio, horaFin, diaOffsetInicio, diaOffsetFin);

        if (franja.DuracionEnMinutos() <= 0)
            throw new ArgumentException(Mensajes.DuracionNoPositiva);

        return franja;
    }

    // DTO plano propio de esta isla; el FA lo mapea a DetalleSubFranja solo para los eventos que cruzan
    // el bus.
    public SubFranjaProgramada ToDetalle() =>
        new(_horaInicio, _horaFin, _diaOffsetInicio, _diaOffsetFin, ToString());

    public override string ToString() =>
        $"{FormatearHora(_horaInicio, _diaOffsetInicio)}-{FormatearHora(_horaFin, _diaOffsetFin)}";

    public bool Equals(SubFranja? other) =>
        other is not null
        && _horaInicio == other._horaInicio
        && _horaFin == other._horaFin
        && _diaOffsetInicio == other._diaOffsetInicio
        && _diaOffsetFin == other._diaOffsetFin;

    public override bool Equals(object? obj) => Equals(obj as SubFranja);

    public override int GetHashCode() =>
        HashCode.Combine(_horaInicio, _horaFin, _diaOffsetInicio, _diaOffsetFin);

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
