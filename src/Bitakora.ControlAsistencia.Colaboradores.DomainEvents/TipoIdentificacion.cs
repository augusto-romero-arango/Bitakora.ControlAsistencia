namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Tipo de documento de identificacion del colaborador -- lista cerrada segun la Resolucion
/// 2388/2016 (PILA): CC, CE, TI, PA, PT.
/// </summary>
/// <remarks>
/// NUNCA un enum C# (MEF-ADR-0012): lo persistido y lo que compone claves de stream es siempre el
/// codigo literal ("CC"), jamas una sombra numerica. Agregar un tipo es agregar una instancia
/// estatica y desplegar, sin migrar datos.
/// Igualdad por referencia (deliberado): Desde() siempre retorna la instancia canonica, nunca crea
/// una nueva.
/// </remarks>
public sealed partial class TipoIdentificacion
{
    public static readonly TipoIdentificacion CC = new("CC");
    public static readonly TipoIdentificacion CE = new("CE");
    public static readonly TipoIdentificacion TI = new("TI");
    public static readonly TipoIdentificacion PA = new("PA");
    public static readonly TipoIdentificacion PT = new("PT");

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

    // El chequeo de null no es redundante: Identificacion.ConfigurarSerializacion llama aqui con el
    // valor crudo del JSON, y un "tipo": null persistido debe fallar con el mensaje de dominio, no
    // con el ArgumentNullException del diccionario.
    // Aceptar "cc" no abre otra clave de stream: Desde() nunca almacena el input, siempre retorna la
    // instancia canonica.
    public static TipoIdentificacion Desde(string codigo) =>
        codigo is not null && PorCodigo.TryGetValue(codigo.Trim().ToUpperInvariant(), out var tipo)
            ? tipo
            : throw new ArgumentException(Mensajes.CodigoNoReconocido, nameof(codigo));

    // Contrato de persistencia y de composicion de la clave de stream.
    public override string ToString() => _codigo;
}
