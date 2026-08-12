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

    // Issue #352: fecha efectiva de terminacion de la vinculacion ANTERIOR a la vigente (la que
    // Apply(VinculacionIniciada) desplaza al reabrir con un reingreso). A diferencia de
    // _fechaTerminacionVinculacionVigente (que Apply(VinculacionIniciada) resetea a null al
    // reabrir), este campo sobrevive el reingreso: es el unico dato que permite evaluar la
    // no-solape hacia atras (CorregirFechaInicio) despues de que la vinculacion anterior dejo de
    // ser la vigente. null cuando nunca hubo una vinculacion anterior (colaborador en su primera
    // vinculacion).
    private DateOnly? _fechaTerminacionVinculacionAnterior;

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

    // Issue #350: reabre la vinculacion al re-aplicarse -- una segunda VinculacionIniciada en el
    // mismo stream (reingreso) deja limpia la terminacion de la vinculacion anterior, sin lo cual
    // el reingreso rehidratado quedaria "ya terminado" heredando la terminacion previa.
    // Issue #352: antes de resetear, conserva en _fechaTerminacionVinculacionAnterior la
    // terminacion de la vinculacion que este reingreso desplaza -- es el unico rastro que
    // CorregirFechaInicio tiene para evaluar la no-solape hacia atras una vez que
    // _fechaTerminacionVinculacionVigente ya se reseteo a null.
    public void Apply(VinculacionIniciada e)
    {
        _fechaTerminacionVinculacionAnterior = _fechaTerminacionVinculacionVigente;
        _codigoVinculacionVigente = e.Codigo;
        _fechaInicioVinculacionVigente = e.FechaInicio;
        _fechaTerminacionVinculacionVigente = null;
    }

    // Issue #349: registra la terminacion de la vinculacion vigente. Nunca lanza (MEF-ADR-0004
    // capa 4).
    public void Apply(VinculacionTerminada e) => _fechaTerminacionVinculacionVigente = e.FechaEfectiva;

    // Issue #354: anula la terminacion registrada de la ULTIMA vinculacion -- la reabre sin tocar
    // su codigo ni su fecha de inicio (ambos quedan intactos, Apply solo limpia la terminacion).
    // Nunca lanza (MEF-ADR-0004 capa 4).
    // STUB (fase roja, issue #354): el cuerpo completo queda para el implementer.
    public void Apply(TerminacionAnulada e) => throw new NotImplementedException();

    // Issue #351: reemplaza el nombre de la persona. Nunca lanza (MEF-ADR-0004 capa 4).
    public void Apply(NombresCorregidos e) => _nombre = e.Nombre;

    // Issue #352: reemplaza la fecha de inicio de la ULTIMA vinculacion (tenga o no terminacion
    // registrada). Nunca lanza (MEF-ADR-0004 capa 4).
    public void Apply(FechaInicioVinculacionCorregida e) => _fechaInicioVinculacionVigente = e.FechaInicio;

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
    // Exito: appendea VinculacionIniciada(codigo, fechaInicio) a _uncommittedEvents y lo aplica --
    // ese Apply reabre la vinculacion (limpia _fechaTerminacionVinculacionVigente), de modo que el
    // ciclo registro-terminacion-reingreso-terminacion es encadenable sin estado residual.
    // internal: mismo criterio de visibilidad que TerminarVinculacion y Registrar.
    internal ResultadoReingresoColaborador Reingresar(string codigo, DateOnly fechaInicio)
    {
        if (_fechaTerminacionVinculacionVigente is null)
            return ResultadoReingresoColaborador.VinculacionAbierta;

        if (fechaInicio <= _fechaTerminacionVinculacionVigente.Value)
            return ResultadoReingresoColaborador.FechaSolapaVinculacionAnterior;

        var evento = new VinculacionIniciada(codigo, fechaInicio);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoReingresoColaborador.Exitosa;
    }

    // Issue #351: mecanismo "declinar en silencio" (precedente ControlDiarioAggregateRoot.
    // AdicionarMarcacion) -- nunca lanza ni emite un evento de fallo persistido, y a diferencia de
    // TerminarVinculacion/Reingresar no responde razon: sin reglas de estado que violar, la unica
    // causa de no emitir es que no haya nada que corregir, y el borde responde 202 igual.
    // La idempotencia es por igualdad de VALOR (NombreColaborador.Equals, #348), no por los
    // primitivos crudos del comando: el handler ya construyo el VO, que normaliza trim y opcionales
    // ausentes antes de que esta comparacion ocurra.
    // No mira la vigencia de la vinculacion: los nombres son de la PERSONA, no de la vinculacion
    // (decision de refinamiento 2026-08-11), asi que corregir sobre una vinculacion terminada es
    // valido. La existencia del colaborador ya la garantizo el handler al rehidratarlo.
    // internal: mismo criterio de visibilidad que TerminarVinculacion/Reingresar.
    internal void CorregirNombres(NombreColaborador nombre)
    {
        if (nombre.Equals(_nombre))
            return;

        var evento = new NombresCorregidos(nombre);
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    // Issue #352: mecanismo combinado (CA-ADR-0030) -- "declinar con resultado" para las dos
    // reglas de estado y "declinar en silencio" (precedente CorregirNombres #351) para la
    // idempotencia. Tres reglas evaluables solo con la historia del stream, sin reloj (decision de
    // refinamiento 2026-08-11):
    //   - SinCambios (idempotencia, se evalua PRIMERO): fechaCorregida == _fechaInicioVinculacionVigente
    //     -> ningun evento, sin excepcion (patron #351: la idempotencia no consulta las demas reglas).
    //   - FechaPosteriorATerminacionPropia: la ULTIMA vinculacion tiene terminacion registrada
    //     (_fechaTerminacionVinculacionVigente is not null) y fechaCorregida >
    //     _fechaTerminacionVinculacionVigente.Value (fechaCorregida == la propia terminacion es
    //     valida: vinculacion de un solo dia, consistente con TerminarVinculacion #349).
    //   - FechaSolapaVinculacionAnterior: no-solape hacia atras, solo ejercitable cuando existe una
    //     vinculacion anterior (tras un reingreso, #350) -- fechaCorregida es igual o anterior a la
    //     FechaEfectiva de esa vinculacion anterior (misma frontera que Reingresar #350: el dia de
    //     la fecha efectiva pertenece a la vinculacion que termino).
    // Exito: appendea FechaInicioVinculacionCorregida a _uncommittedEvents y lo aplica.
    // internal: mismo criterio de visibilidad que TerminarVinculacion/Reingresar/CorregirNombres --
    // el unico llamador es el handler del mismo ensamblado (los tests lo alcanzan via
    // InternalsVisibleTo).
    internal ResultadoCorreccionFechaInicioVinculacion CorregirFechaInicio(DateOnly fechaCorregida)
    {
        if (fechaCorregida == _fechaInicioVinculacionVigente)
            return ResultadoCorreccionFechaInicioVinculacion.SinCambios;

        if (_fechaTerminacionVinculacionVigente is not null &&
            fechaCorregida > _fechaTerminacionVinculacionVigente.Value)
            return ResultadoCorreccionFechaInicioVinculacion.FechaPosteriorATerminacionPropia;

        if (_fechaTerminacionVinculacionAnterior is not null &&
            fechaCorregida <= _fechaTerminacionVinculacionAnterior.Value)
            return ResultadoCorreccionFechaInicioVinculacion.FechaSolapaVinculacionAnterior;

        var evento = new FechaInicioVinculacionCorregida(fechaCorregida);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoCorreccionFechaInicioVinculacion.Exitosa;
    }

    // Issue #354: mecanismo "declinar con resultado" (CA-ADR-0030) -- nunca lanza, nunca emite un
    // evento de fallo persistido. Unica regla, evaluable solo con la historia del stream, sin
    // reloj (decision de refinamiento 2026-08-11 -- el arrepentimiento del preaviso y la fecha de
    // terminacion errada comparten esta misma solucion):
    //   - VinculacionAbierta: _fechaTerminacionVinculacionVigente is null -- cubre tres casos que
    //     el handler no distingue entre si (recien registrada, reingresada, o ya anulada antes,
    //     CA-3/CA-4): tras un reingreso la terminacion de la vinculacion ANTERIOR queda congelada
    //     (decision aprobada explicitamente) porque solo la ULTIMA vinculacion cuenta.
    // Exito: appendea TerminacionAnulada a _uncommittedEvents y lo aplica -- reabre la vinculacion
    // vigente con su codigo y fecha de inicio intactos (Apply no los toca).
    // internal: mismo criterio de visibilidad que TerminarVinculacion/Reingresar/CorregirNombres/
    // CorregirFechaInicio -- el unico llamador es el handler del mismo ensamblado (los tests lo
    // alcanzan via InternalsVisibleTo).
    // STUB (fase roja, issue #354): el cuerpo completo queda para el implementer.
    internal ResultadoAnulacionTerminacion AnularTerminacion() => throw new NotImplementedException();

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
