using System.Reflection;
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
    public static Identificacion Crear(TipoIdentificacion tipo, string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            throw new ArgumentException(Mensajes.NumeroVacio, nameof(numero));

        return new Identificacion(tipo, numero.Trim().ToUpperInvariant());
    }

    // Contrato: clave del stream de Colaborador.
    public override string ToString() => $"{_tipo}:{_numero}";

    // Igualdad por valor. _tipo se compara por referencia (deliberado, ver remarks de
    // TipoIdentificacion): Desde() siempre retorna la instancia canonica de la lista cerrada.
    public bool Equals(Identificacion? other) =>
        other is not null && _tipo == other._tipo && _numero == other._numero;

    public override bool Equals(object? obj) => Equals(obj as Identificacion);

    public override int GetHashCode() => HashCode.Combine(_tipo, _numero);

    // Mapping de serializacion -- vive aqui porque cambia con la clase (patron SubFranja).
    // _tipo se persiste como su codigo literal ("CC") y se rehidrata via TipoIdentificacion.Desde --
    // el codigo, no una sombra numerica, es el contrato de identidad (MEF-ADR-0012).
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(Identificacion)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        var tipoField = typeof(Identificacion)
            .GetField("_tipo", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var numeroField = typeof(Identificacion)
            .GetField("_numero", BindingFlags.NonPublic | BindingFlags.Instance)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(Identificacion)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => ctor.Invoke(null);

            var tipoProp = typeInfo.CreateJsonPropertyInfo(typeof(string), "tipo");
            tipoProp.Get = obj => ((Identificacion)obj)._tipo.ToString();
            tipoProp.Set = (obj, val) => tipoField.SetValue(obj, TipoIdentificacion.Desde((string)val!));
            typeInfo.Properties.Add(tipoProp);

            var numeroProp = typeInfo.CreateJsonPropertyInfo(typeof(string), "numero");
            numeroProp.Get = obj => numeroField.GetValue(obj);
            numeroProp.Set = (obj, val) => numeroField.SetValue(obj, val);
            typeInfo.Properties.Add(numeroProp);
        });
    }
}
