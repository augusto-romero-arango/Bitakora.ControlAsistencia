using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #330: aggregate root que representa a un colaborador bajo control de asistencia. Nace con
// este issue -- primer aggregate y primeros dos eventos persistidos del dominio Colaboradores.
//
// Identidad: Identificacion.ToString() ("CC:79543210"), no un Guid. ComputarStreamId es el punto
// unico de conversion de la clave del stream (MEF-ADR-0037): ningun handler/endpoint la concatena.
//
// Estado observable: internal, no publico (InternalsVisibleTo hacia Colaboradores.Tests, ver el
// .csproj). El issue no expone lectura de la vinculacion al exterior del ensamblado; los observables
// existen para que el DSL de tests (And<>) verifique el estado rehidratado sin ampliar la superficie
// publica del dominio. #349+ decidiran cuales necesitan sus invariantes (no-solape, maximo una
// vigente).
//
// partial (MEF-ADR-0009): admite una clase Mensajes en archivo separado el dia que el aggregate
// tenga eventos de fallo propios. En este corte no tiene ninguno -- "Identificacion ya registrada"
// es precondicion de orquestacion del handler (MEF-ADR-0004 capa 2), no regla de negocio del
// aggregate.
public partial class ColaboradorAggregateRoot : AggregateRoot
{
    private Identificacion? _identificacion;
    private NombreColaborador? _nombre;
    private string? _codigoVinculacionVigente;
    private DateOnly _fechaInicioVinculacionVigente;
    private DateOnly? _fechaTerminacionVinculacionVigente;

    internal Identificacion Identificacion => _identificacion!;
    internal NombreColaborador Nombre => _nombre!;
    internal string CodigoVinculacionVigente => _codigoVinculacionVigente!;
    internal DateOnly FechaInicioVinculacionVigente => _fechaInicioVinculacionVigente;

    // Issue #349: null mientras la vinculacion vigente esta abierta; con valor una vez que
    // TerminarVinculacion tuvo exito (registro tardio o preaviso, sin distincion de estado). No
    // publico: es insumo del DSL de tests (And<>), no lectura expuesta a otros consumidores del
    // ensamblado (Tell-don't-Ask, MEF-ADR-0012) -- el handler decide unicamente por el resultado
    // que TerminarVinculacion le responde, nunca interrogando este campo.
    internal DateOnly? FechaTerminacionVinculacionVigente => _fechaTerminacionVinculacionVigente;

    // Contrato: clave del stream de Colaborador. Delega en el ToString() canonico del VO (#348) --
    // ningun handler/endpoint concatena la clave por su cuenta (MEF-ADR-0037).
    public static string ComputarStreamId(Identificacion identificacion) => identificacion.ToString();

    // Apply: publicos -- requerido para que TestStore.ApplyEvent los encuentre via GetMethods().
    // Nunca lanzan (MEF-ADR-0004 capa 4).
    public void Apply(ColaboradorRegistrado e)
    {
        Id = ComputarStreamId(e.Identificacion);
        _identificacion = e.Identificacion;
        _nombre = e.Nombre;
    }

    public void Apply(VinculacionIniciada e)
    {
        _codigoVinculacionVigente = e.Codigo;
        _fechaInicioVinculacionVigente = e.FechaInicio;
    }

    // Issue #349: registra la terminacion de la vinculacion vigente. Nunca lanza (MEF-ADR-0004
    // capa 4) -- STUB (fase roja): el implementer asigna _fechaTerminacionVinculacionVigente =
    // e.FechaEfectiva.
    public void Apply(VinculacionTerminada e) => throw new NotImplementedException();

    // Issue #349: mecanismo "declinar con resultado" (CA-ADR-0030) -- nunca lanza, nunca emite un
    // evento de fallo persistido. Dos razones de rechazo evaluables solo con la historia del
    // stream, sin reloj (decision de refinamiento):
    //   - YaTerminada: _fechaTerminacionVinculacionVigente ya tiene valor (incluye un preaviso
    //     cuya fecha aun no llego -- "ya terminada" es "tiene terminacion registrada", no "la
    //     fecha ya paso").
    //   - FechaAnteriorAInicio: fechaEfectiva < _fechaInicioVinculacionVigente (duracion
    //     negativa). fechaEfectiva == _fechaInicioVinculacionVigente es valida (vinculacion de un
    //     solo dia).
    // Exito: appendea VinculacionTerminada a _uncommittedEvents y lo aplica.
    // STUB (fase roja, issue #349): el cuerpo completo queda para el implementer.
    public ResultadoTerminacionVinculacion TerminarVinculacion(DateOnly fechaEfectiva) =>
        throw new NotImplementedException();

    // Factory interno: agrega los DOS eventos del commit a _uncommittedEvents y los aplica -- patron
    // RegistroDeMarcacionAggregateRoot.Iniciar, generalizado a dos eventos en el mismo commit.
    internal static ColaboradorAggregateRoot Registrar(
        Identificacion identificacion, NombreColaborador nombre, string codigo, DateOnly fechaInicio)
    {
        var colaborador = new ColaboradorAggregateRoot();

        var colaboradorRegistrado = new ColaboradorRegistrado(identificacion, nombre);
        colaborador._uncommittedEvents.Add(colaboradorRegistrado);
        colaborador.Apply(colaboradorRegistrado);

        var vinculacionIniciada = new VinculacionIniciada(codigo, fechaInicio);
        colaborador._uncommittedEvents.Add(vinculacionIniciada);
        colaborador.Apply(vinculacionIniciada);

        return colaborador;
    }
}
