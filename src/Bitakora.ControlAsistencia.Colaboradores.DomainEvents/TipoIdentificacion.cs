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

    private readonly string _codigo;

    private TipoIdentificacion(string codigo) => _codigo = codigo;

    // CA-2: rehidrata desde el codigo persistido; rechaza codigos fuera de la lista cerrada.
    public static TipoIdentificacion Desde(string codigo) => throw new NotImplementedException();

    // El codigo literal ("CC") -- contrato de persistencia y de composicion de la clave de stream.
    public override string ToString() => throw new NotImplementedException();
}
