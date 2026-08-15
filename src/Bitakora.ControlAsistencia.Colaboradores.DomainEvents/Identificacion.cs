using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Identidad de un colaborador: tipo de documento (lista cerrada PILA) + numero.
/// </summary>
/// <remarks>
/// Issue #348: compone la clave del stream del futuro ColaboradorAggregateRoot (#349+) --
/// ToString() -> "{Tipo}-{Numero}" es CONTRATO, no detalle de presentacion (separador "-" desde el
/// issue #381: ":" es hostil como segmento de URI). El numero es alfanumerico (los pasaportes
/// traen letras); se LIMPIA (issue #381) eliminando todo caracter fuera de [A-Za-z0-9] y llevando
/// las letras a MAYUSCULAS -- no se rechaza por caracteres invalidos, se limpian antes de
/// registrar. Esto hace inequivoca la llave "{Tipo}-{Numero}": tras la limpieza el numero jamas
/// contiene "-", y ademas unifica identidad (" ab-12.3 " y "AB123" producen el mismo stream, CA-1).
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

    // Observables de solo lectura (interfaz publica fijada por el issue #348). Son insumo del
    // mapeo rico->plano de #349+, NO la via para armar la clave del stream: esa clave sale
    // unicamente de ToString(). Recomponerla a mano -- $"{id.Tipo}:{id.Numero}" -- duplicaria el
    // punto de conversion y dejaria el separador y el orden en manos del llamador
    // (MEF-ADR-0037: punto unico de conversion de la identidad de stream).
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

    // CA-2: limpia el numero (elimina todo caracter fuera de [A-Za-z0-9], letras a MAYUSCULAS) y
    // valida que el resultado no quede vacio (CA-4). No hay rechazo por caracteres invalidos: se
    // limpian antes de registrar, sin excepcion. El trim de #348 queda subsumido -- los espacios
    // son caracteres invalidos y se eliminan igual que un guion o un punto.
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

    // Contrato: clave del stream de Colaborador (separador "-" desde el issue #381).
    public override string ToString() => $"{_tipo}-{_numero}";

    // Issue #376 (CA-3/CA-5, MEF-ADR-0037 seccion 2): inverso de ToString() -- el UNICO punto de
    // parseo string->Identificacion, para que el borde HTTP (colaboradores/{id}/...) nunca reciba
    // el id de ruta ya armado y lo reenvie sin tipar. Split en la PRIMERA ocurrencia de "-": el tipo
    // (lista cerrada PILA, "CC"/"CE"/"TI"/"PA"/"PT") jamas trae guion, y el numero limpio (issue
    // #381) tampoco -- la posicion del primer separador es inequivoca, sin importar si el numero
    // reconstruido a partir de una entrada cruda del cliente llegara con mas guiones (Crear() los
    // limpia igual). Rechaza con ArgumentException (mismo tipo, un unico punto de traduccion a 400
    // en el endpoint, precedente ObtenerFichaColaborador.FunctionEndpoint) cuando: (a) no hay guion
    // en absoluto, (b) el tipo no esta en la lista cerrada (TipoIdentificacion.Desde ya lo rechaza
    // con su propio mensaje), o (c) el numero queda vacio tras la limpieza (Crear ya lo rechaza con
    // su propio mensaje). Round-trip: Parsear(id.ToString()) es igual por valor a id (CA-5) porque
    // ToString() siempre produce un numero ya limpio ([A-Z0-9]), y Crear() no lo altera de nuevo.
    // NOTA (implementer #377): #376 (PR #388) aun no esta mergeado a main en este worktree -- esta
    // implementacion replica exactamente el contrato ya fijado y documentado en la rama
    // worktree-issue-376-adoptar-verbos-canonicos-en-los-endpoint (misma firma, mismo cuerpo,
    // mismos mensajes) para satisfacer los tests de #377, que dependen de #376. Al mergear #376 a
    // main se espera un conflicto trivial en este metodo (y en Identificacion.Mensajes.cs /
    // IdentificacionMensajes.resx) que se resuelve tomando la version de #376 -- contenido
    // identico, sin perdida.
    public static Identificacion Parsear(string valor)
    {
        var indiceSeparador = valor?.IndexOf('-') ?? -1;
        if (indiceSeparador < 0)
            throw new ArgumentException(Mensajes.FormatoInvalido, nameof(valor));

        var tipo = TipoIdentificacion.Desde(valor![..indiceSeparador]);
        var numero = valor[(indiceSeparador + 1)..];

        return Crear(tipo, numero);
    }

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
        var tipoClase = typeof(Identificacion);
        var ctor = tipoClase.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        var tipoField = tipoClase.GetField("_tipo", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var numeroField = tipoClase.GetField("_numero", BindingFlags.NonPublic | BindingFlags.Instance)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != tipoClase) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => ctor.Invoke(null);

            // Los Get leen los campos directamente (este codigo vive dentro de la propia clase);
            // solo los Set necesitan reflexion, porque los campos son readonly y C# no permite
            // asignarlos fuera del constructor.
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
