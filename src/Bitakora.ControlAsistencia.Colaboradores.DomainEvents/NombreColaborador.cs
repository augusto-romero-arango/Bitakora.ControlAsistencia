using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Nombre completo del colaborador -- dueno unico de la composicion del nombre completo.
/// </summary>
/// <remarks>
/// Issue #348: primer nombre y primer apellido son obligatorios (minimo colombiano); los segundos
/// son opcionales (null, vacio o whitespace se normalizan a ausente, con trim en los presentes).
/// NombreCompleto es la unica composicion canonica -- ningun consumidor concatena por su cuenta
/// (Tell-don't-Ask, MEF-ADR-0012).
/// Los 4 componentes NO son publicos: cuando el futuro evento persistido (#349+) necesite mapear a
/// un DTO plano de bus, ese issue decidira la via de conversion (ej. ToContrato()), no este.
/// </remarks>
public sealed partial class NombreColaborador : IEquatable<NombreColaborador>
{
    private readonly string _primerNombre;
    private readonly string? _segundoNombre;
    private readonly string _primerApellido;
    private readonly string? _segundoApellido;

    private NombreColaborador(
        string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido)
    {
        _primerNombre = primerNombre;
        _segundoNombre = segundoNombre;
        _primerApellido = primerApellido;
        _segundoApellido = segundoApellido;
    }

    // Constructor vacio para STJ/Marten
    private NombreColaborador()
    {
        _primerNombre = string.Empty;
        _primerApellido = string.Empty;
    }

    // CA-3: primer nombre y primer apellido obligatorios (no vacios ni whitespace, con trim). Los
    // segundos se normalizan a ausente cuando llegan null, vacios o whitespace.
    public static NombreColaborador Crear(
        string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido)
    {
        if (string.IsNullOrWhiteSpace(primerNombre))
            throw new ArgumentException(Mensajes.PrimerNombreRequerido, nameof(primerNombre));
        if (string.IsNullOrWhiteSpace(primerApellido))
            throw new ArgumentException(Mensajes.PrimerApellidoRequerido, nameof(primerApellido));

        return new NombreColaborador(
            primerNombre.Trim(), NormalizarOpcional(segundoNombre),
            primerApellido.Trim(), NormalizarOpcional(segundoApellido));
    }

    // CA-4: composicion canonica unica -- espacios simples, omite opcionales ausentes.
    public string NombreCompleto =>
        string.Join(" ", new[] { _primerNombre, _segundoNombre, _primerApellido, _segundoApellido }
            .Where(componente => !string.IsNullOrWhiteSpace(componente)));

    public override string ToString() => NombreCompleto;

    // Igualdad por valor
    public bool Equals(NombreColaborador? other) =>
        other is not null
        && _primerNombre == other._primerNombre
        && _segundoNombre == other._segundoNombre
        && _primerApellido == other._primerApellido
        && _segundoApellido == other._segundoApellido;

    public override bool Equals(object? obj) => Equals(obj as NombreColaborador);

    public override int GetHashCode() =>
        HashCode.Combine(_primerNombre, _segundoNombre, _primerApellido, _segundoApellido);

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipoClase = typeof(NombreColaborador);
        var ctor = tipoClase.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        var primerNombreField = tipoClase.GetField("_primerNombre", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var segundoNombreField = tipoClase.GetField("_segundoNombre", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var primerApellidoField = tipoClase.GetField("_primerApellido", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var segundoApellidoField = tipoClase.GetField("_segundoApellido", BindingFlags.NonPublic | BindingFlags.Instance)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != tipoClase) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => ctor.Invoke(null);

            RegistrarCampo(typeInfo, primerNombreField, "primerNombre");
            RegistrarCampo(typeInfo, segundoNombreField, "segundoNombre");
            RegistrarCampo(typeInfo, primerApellidoField, "primerApellido");
            RegistrarCampo(typeInfo, segundoApellidoField, "segundoApellido");
        });
    }

    private static void RegistrarCampo(JsonTypeInfo typeInfo, FieldInfo field, string nombreJson)
    {
        var prop = typeInfo.CreateJsonPropertyInfo(typeof(string), nombreJson);
        prop.Get = obj => field.GetValue(obj);
        prop.Set = (obj, val) => field.SetValue(obj, val);
        typeInfo.Properties.Add(prop);
    }
}
