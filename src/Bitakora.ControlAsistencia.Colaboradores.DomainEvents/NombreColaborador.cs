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
        string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido) =>
        throw new NotImplementedException();

    // CA-4: composicion canonica unica -- espacios simples, omite opcionales ausentes.
    public string NombreCompleto => throw new NotImplementedException();

    public override string ToString() => throw new NotImplementedException();

    // Igualdad por valor
    public bool Equals(NombreColaborador? other) => throw new NotImplementedException();

    public override bool Equals(object? obj) => Equals(obj as NombreColaborador);

    public override int GetHashCode() => throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
