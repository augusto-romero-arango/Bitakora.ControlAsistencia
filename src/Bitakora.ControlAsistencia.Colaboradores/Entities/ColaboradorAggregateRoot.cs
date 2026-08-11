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
    // capa 4).
    public void Apply(VinculacionTerminada e) => _fechaTerminacionVinculacionVigente = e.FechaEfectiva;

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
    // internal, como Registrar y como los metodos de comando de los demas aggregates del repo: el
    // unico llamador es el handler del mismo ensamblado (los tests lo alcanzan via InternalsVisibleTo).
    internal ResultadoTerminacionVinculacion TerminarVinculacion(DateOnly fechaEfectiva)
    {
        if (_fechaTerminacionVinculacionVigente is not null)
            return ResultadoTerminacionVinculacion.YaTerminada;

        if (fechaEfectiva < _fechaInicioVinculacionVigente)
            return ResultadoTerminacionVinculacion.FechaAnteriorAInicio;

        var evento = new VinculacionTerminada(fechaEfectiva);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoTerminacionVinculacion.Exitosa;
    }

    // Issue #350: mecanismo "declinar con resultado" (CA-ADR-0030) -- nunca lanza, nunca emite un
    // evento de fallo persistido. Reutiliza VinculacionIniciada (CA-ADR-0029: un evento no conoce
    // su comando) -- mismo hecho que Registrar, comando distinto.
    // Dos razones de rechazo evaluables solo con la historia del stream, sin reloj (invariante de
    // no-solape, doctrina del preaviso #349):
    //   - VinculacionAbierta: _fechaTerminacionVinculacionVigente is null (incluye un reingreso
    //     previo sin terminar).
    //   - FechaSolapaVinculacionAnterior: fechaInicio <= _fechaTerminacionVinculacionVigente.Value
    //     (estrictamente posterior es la unica fecha valida -- el mismo dia se rechaza).
    // Exito: appendea VinculacionIniciada(codigo, fechaInicio) a _uncommittedEvents y lo aplica.
    // NOTA para el implementer: Apply(VinculacionIniciada) debe reabrir la vinculacion (limpiar
    // _fechaTerminacionVinculacionVigente) al re-aplicarse -- si #330 lo dejo asumiendo una unica
    // aplicacion, ajustarlo es parte natural del alcance de este issue (ver comentario de #330 en
    // Apply(VinculacionIniciada) mas arriba).
    // internal: mismo criterio de visibilidad que TerminarVinculacion y Registrar.
    // STUB (fase roja, issue #350): el cuerpo completo queda para el implementer.
    internal ResultadoReingresoColaborador Reingresar(string codigo, DateOnly fechaInicio) =>
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
