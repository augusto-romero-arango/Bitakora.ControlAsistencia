using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Identidad de un colaborador: tipo de documento (lista cerrada PILA) + numero.
/// </summary>
/// <remarks>
/// Issue #348: compone la clave del stream del futuro ColaboradorAggregateRoot (#349+) --
/// ToString() -> "{Tipo}:{Numero}" es CONTRATO, no detalle de presentacion. El numero es
/// alfanumerico (los pasaportes traen letras), se normaliza trim + MAYUSCULAS para que
/// " ab-123 " y "AB-123" produzcan el mismo stream (CA-1).
/// MEF-ADR-0012: sealed class, constructor privado real + constructor vacio para STJ/Marten,
/// factory Crear() con validacion, ConfigurarSerializacion registrando campos privados (patron
/// SubFranja) -- proscrito [JsonConstructor].
/// Tipo y Numero son observables de solo lectura: son la identidad misma (no datos intermedios de
/// calculo), los DTOs planos de bus del futuro evento persistido (#349+) los necesitaran para el
/// mapeo rico->plano.
/// </remarks>
public sealed partial class Identificacion : IEquatable<Identificacion>
{
    private readonly TipoIdentificacion _tipo;
    private readonly string _numero;

    public TipoIdentificacion Tipo => _tipo;
    public string Numero => _numero;

    private Identificacion(TipoIdentificacion tipo, string numero)
    {
        _tipo = tipo;
        _numero = numero;
    }

    // Constructor vacio para STJ/Marten
    private Identificacion()
    {
        _tipo = TipoIdentificacion.CC;
        _numero = string.Empty;
    }

    // CA-1, CA-2: normaliza (trim + MAYUSCULAS) y valida que el numero no sea vacio ni whitespace.
    public static Identificacion Crear(TipoIdentificacion tipo, string numero) =>
        throw new NotImplementedException();

    // Contrato: clave del stream de Colaborador.
    public override string ToString() => throw new NotImplementedException();

    // Igualdad por valor
    public bool Equals(Identificacion? other) => throw new NotImplementedException();

    public override bool Equals(object? obj) => Equals(obj as Identificacion);

    public override int GetHashCode() => throw new NotImplementedException();

    // Mapping de serializacion -- vive aqui porque cambia con la clase (patron SubFranja).
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
