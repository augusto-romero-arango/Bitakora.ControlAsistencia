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

    // STUB agregado por el test-writer del issue #377 (Exponer la correccion de nombres como PUT
    // del recurso nombres) -- NO es alcance de #377. #377 depende de #376 (Adoptar verbos canonicos
    // en los endpoints de etiquetas, label "bloqueado"), que introduce Parsear como el UNICO punto
    // de parseo string->Identificacion para el borde HTTP (colaboradores/{id}/...). #376 (PR #388)
    // aun no esta mergeado a main en el momento de escribir estos tests -- este worktree parte de
    // main sin ese cambio, asi que Parsear no existe todavia en esta rama. Se agrega este stub
    // minimo (regla 1 del test-writer: solo NotImplementedException, sin logica real) unicamente
    // para que CorregirNombresFunction/FunctionEndpoint.cs y sus tests (issue #377) compilen.
    // Cuando #376 se mergee a main, este metodo entrara en conflicto con su implementacion real
    // (ver la rama worktree-issue-376-adoptar-verbos-canonicos-en-los-endpoint, misma firma
    // exacta) -- el conflicto se resuelve descartando ESTE stub a favor de esa implementacion real.
    // Ver docs/adr/mef-adr-0037 seccion 2 para el contrato completo que la implementacion real debe
    // cumplir (split en la primera ocurrencia de "-", TipoIdentificacion.Desde + Identificacion.Crear).
    public static Identificacion Parsear(string valor) => throw new NotImplementedException();

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
