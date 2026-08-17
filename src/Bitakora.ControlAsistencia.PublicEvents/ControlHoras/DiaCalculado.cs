using Bitakora.ControlAsistencia.PublicEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PublicEvents.ControlHoras;

// HU-108: Evento publico que se publica al Service Bus via IPublicEventSender.
// Representa el resultado del calculo del dia de trabajo de un colaborador tras una
// marcacion o asignacion de turno.
// Issue #340: el payload paso de InformacionEmpleado a InformacionColaborador (termino proscrito
// por #330) -- solo el TIPO, sin tocar la clave JSON.
// Issue #401: la propiedad InformacionEmpleado paso a InformacionColaborador y su campo EmpleadoId
// a CodigoColaborador. Aqui SI cambian las claves JSON que viajan por el bus hacia el consumidor
// de nomina; sin mapeo, el vocabulario del contrato publico queda unificado con el del maestro
// Colaboradores (#330).
// Issue #183: el payload es 100% primitivo (HorasDiscriminadas). Se elimino el modelo rico
// (ControlesDeFranja: IReadOnlyList<DetalleControlFranja> y el antiguo DesgloseHoras) que dependia
// del resolver custom de Marten y se serializaba lossy en el canal de publicacion a Service Bus.
// ADR-0015: sigue siendo sealed class (no record) por contener HorasDiscriminadas con colecciones.
// Un record prometeria igualdad por valor que no cumple con sus IReadOnlyDictionary/IReadOnlyList.
// Patron DTO de integracion: constructor publico parametrizado + privado vacio.
// Sin ConfigurarSerializacion propio: STJ (de)serializa via el ctor publico y propiedades publicas,
// con el serializador POR DEFECTO del publisher (sin resolver custom) -- esa es la cura del bug.
// ADR-0002: vive en Contracts por ser contrato cross-domain (consumidor: sistema de nomina).
// ADR-0004: topic "dia-calculado". ADR-0005: naming kebab-case, participio pasado.
public sealed class DiaCalculado : IPublicEvent
{
    // Puede ser null cuando el ControlDiario nacio solo por marcacion sin turno previo.
    public InformacionColaborador? InformacionColaborador { get; private set; }
    public DateOnly Fecha { get; private set; }
    public HorasDiscriminadas HorasDiscriminadas { get; private set; } = null!;

    public DiaCalculado(
        InformacionColaborador? informacionColaborador,
        DateOnly fecha,
        HorasDiscriminadas horasDiscriminadas)
    {
        InformacionColaborador = informacionColaborador;
        Fecha = fecha;
        HorasDiscriminadas = horasDiscriminadas;
    }

    // Constructor privado para STJ/Marten (ADR-0015 patron DTO de integracion sin invariantes).
    // STJ usa este ctor para crear la instancia y luego asigna las propiedades via setters.
    private DiaCalculado() { }
}
