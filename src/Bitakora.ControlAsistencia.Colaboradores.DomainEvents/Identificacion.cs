using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Identidad de un colaborador: tipo de documento (lista cerrada PILA) + numero.
/// </summary>
/// <remarks>
/// ToString() -> "{Tipo}-{Numero}" es la clave del stream de Colaborador: CONTRATO, no presentacion
/// (separador "-" porque ":" es hostil como segmento de URI).
/// El numero es alfanumerico (los pasaportes traen letras) y se LIMPIA en vez de rechazarse: solo
/// [A-Z0-9], letras en mayuscula. Asi el numero jamas contiene "-" (la llave es inequivoca) y
/// " ab-12.3 " y "AB123" producen el mismo stream.
/// </remarks>
public sealed partial class Identificacion : IEquatable<Identificacion>
{
    private readonly TipoIdentificacion _tipo;
    private readonly string _numero;

    // No son la via para armar la clave del stream: esa sale solo de ToString() (MEF-ADR-0037,
    // punto unico de conversion); recomponerla a mano deja separador y orden en manos del llamador.
    public TipoIdentificacion Tipo => _tipo;
    public string Numero => _numero;

    private Identificacion(TipoIdentificacion tipo, string numero)
    {
        _tipo = tipo;
        _numero = numero;
    }

    // Para STJ/Marten
    private Identificacion()
    {
        _tipo = TipoIdentificacion.CC;
        _numero = string.Empty;
    }

    public static Identificacion Crear(TipoIdentificacion tipo, string numero)
    {
        var numeroLimpio = new string((numero ?? string.Empty)
            .Where(char.IsAsciiLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (numeroLimpio.Length == 0)
            throw new ArgumentException(Mensajes.NumeroVacio, nameof(numero));

        return new Identificacion(tipo, numeroLimpio);
    }

    // Contrato: clave del stream de Colaborador.
    public override string ToString() => $"{_tipo}-{_numero}";

    // Inverso de ToString() y UNICO punto de parseo string->Identificacion (MEF-ADR-0037 seccion 2).
    // Corta en el PRIMER "-": el tipo nunca trae guion, y los guiones de mas en el numero los limpia
    // Crear(). Todo rechazo es ArgumentException, para que el endpoint traduzca a 400 en un solo
    // punto. Parsear(id.ToString()) es igual por valor a id.
    public static Identificacion Parsear(string valor)
    {
        var indiceSeparador = valor?.IndexOf('-') ?? -1;
        if (indiceSeparador < 0)
            throw new ArgumentException(Mensajes.FormatoInvalido, nameof(valor));

        var tipo = TipoIdentificacion.Desde(valor![..indiceSeparador]);
        var numero = valor[(indiceSeparador + 1)..];

        return Crear(tipo, numero);
    }

    // _tipo se compara por referencia (deliberado): Desde() siempre retorna la instancia canonica
    // de la lista cerrada.
    public bool Equals(Identificacion? other) =>
        other is not null && _tipo == other._tipo && _numero == other._numero;

    public override bool Equals(object? obj) => Equals(obj as Identificacion);

    public override int GetHashCode() => HashCode.Combine(_tipo, _numero);

    // _tipo se persiste como su codigo literal ("CC") y se rehidrata via TipoIdentificacion.Desde:
    // el codigo, no una sombra numerica, es el contrato de identidad (MEF-ADR-0012).
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipoClase = typeof(Identificacion);
        var ctor = tipoClase.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        var tipoField = tipoClase.GetField("_tipo", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var numeroField = tipoClase.GetField("_numero", BindingFlags.NonPublic | BindingFlags.Instance)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != tipoClase) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => ctor.Invoke(null);

            var tipoProp = typeInfo.CreateJsonPropertyInfo(typeof(string), "tipo");
            tipoProp.Get = obj => ((Identificacion)obj)._tipo.ToString();
            tipoProp.Set = (obj, val) => tipoField.SetValue(obj, TipoIdentificacion.Desde((string)val!));
            typeInfo.Properties.Add(tipoProp);

            var numeroProp = typeInfo.CreateJsonPropertyInfo(typeof(string), "numero");
            numeroProp.Get = obj => ((Identificacion)obj)._numero;
            numeroProp.Set = (obj, val) => numeroField.SetValue(obj, val);
            typeInfo.Properties.Add(numeroProp);
        });
    }
}
