namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Tipo de documento de identificacion del colaborador -- lista cerrada segun la Resolucion
/// 2388/2016 (PILA): CC, CE, TI, PA, PT.
/// </summary>
/// <remarks>
/// Issue #348: VO de lista cerrada -- instancias estaticas con constructor privado (MEF-ADR-0012),
/// NUNCA un enum C#: lo persistido y lo que compone claves de stream es siempre el codigo literal
/// ("CC"), jamas una sombra numerica. Extensible por despliegue: agregar un tipo de documento es
/// agregar una instancia estatica y desplegar, sin migrar datos existentes.
/// Nota: TipoBloque (ControlHoras.DomainEvents) es enum, pero no es precedente aplicable aqui -- no
/// compone claves de stream ni persiste como codigo literal.
/// Igualdad por referencia (deliberado, decision de pipeline): Desde() siempre retorna la instancia
/// canonica de la lista cerrada, nunca crea una nueva, asi que no hace falta IEquatable custom. La
/// interfaz publica propuesta por el planner dejaba esta decision explicitamente revisable.
/// </remarks>
public sealed partial class TipoIdentificacion
{
    public static readonly TipoIdentificacion CC = new("CC");
    public static readonly TipoIdentificacion CE = new("CE");
    public static readonly TipoIdentificacion TI = new("TI");
    public static readonly TipoIdentificacion PA = new("PA");
    public static readonly TipoIdentificacion PT = new("PT");

    // Lookup map por clave discreta (codigo -> instancia), no switch/if: agregar un tipo de
    // documento nuevo es agregar una fila aqui, no una rama.
    private static readonly IReadOnlyDictionary<string, TipoIdentificacion> PorCodigo =
        new Dictionary<string, TipoIdentificacion>
        {
            [CC._codigo] = CC,
            [CE._codigo] = CE,
            [TI._codigo] = TI,
            [PA._codigo] = PA,
            [PT._codigo] = PT,
        };

    private readonly string _codigo;

    private TipoIdentificacion(string codigo) => _codigo = codigo;

    // CA-2: rehidrata desde el codigo persistido; rechaza codigos fuera de la lista cerrada.
    // El chequeo de null NO es defensivo redundante: este es el boundary de rehidratacion desde
    // JSON (Identificacion.ConfigurarSerializacion lo llama con el valor crudo del payload), asi
    // que un "tipo": null persistido debe fallar con el mensaje de dominio y no con el
    // ArgumentNullException del diccionario, que no dice nada del contrato roto. El null-check se
    // evalua ANTES de normalizar -- no se puede invocar Trim() sobre null.
    // Issue #371: normaliza (trim + MAYUSCULAS invariante) antes del lookup -- el conocimiento de
    // como normalizar la entrada pertenece al VO (Tell-don't-Ask, MEF-ADR-0012), no a los callers.
    // El racional de #348 (aceptar "cc" abriria dos representaciones/claves de stream distintas) no
    // se sostiene con el diseno actual: Desde() nunca almacena el input, siempre retorna la
    // instancia canonica de la lista cerrada, y la clave de stream se compone desde esa instancia
    // canonica -- nunca desde el input crudo.
    public static TipoIdentificacion Desde(string codigo) =>
        codigo is not null && PorCodigo.TryGetValue(codigo.Trim().ToUpperInvariant(), out var tipo)
            ? tipo
            : throw new ArgumentException(Mensajes.CodigoNoReconocido, nameof(codigo));

    // El codigo literal ("CC") -- contrato de persistencia y de composicion de la clave de stream.
    public override string ToString() => _codigo;
}
