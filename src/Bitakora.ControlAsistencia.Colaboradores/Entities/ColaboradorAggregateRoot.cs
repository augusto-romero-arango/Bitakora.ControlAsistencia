using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #330: aggregate root que representa a un colaborador bajo control de asistencia. Nace con
// este issue -- primer aggregate y primeros dos eventos persistidos del dominio Colaboradores.
//
// Identidad: Identificacion.ToString() ("CC-79543210"), no un Guid. ComputarStreamId es el punto
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

    // Issue #355: diccionario de etiquetas dinamicas de la vinculacion vigente, clave = categoria
    // normalizada (Etiqueta.CategoriaNormalizada, #353) -- un valor por categoria (AsignarEtiqueta y
    // su Apply sobrescriben la entrada, nunca duplican). No es observable publico: el issue no
    // expone lectura de etiquetas al exterior del ensamblado (Tell-don't-Ask, MEF-ADR-0012) -- la
    // lectura llega con las projections (#356/#357). internal solo para que el DSL de tests (And<>)
    // verifique el estado rehidratado, mismo criterio que los demas observables internos de esta
    // clase.
    private readonly Dictionary<string, Etiqueta> _etiquetas = new();

    internal IReadOnlyDictionary<string, Etiqueta> Etiquetas => _etiquetas;

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
    // Issue #355 (CA-6, "reingreso nace limpio"): ademas vacia las etiquetas -- la vinculacion nueva
    // no hereda las de la anterior (las etiquetas describen la relacion laboral vigente). Se vacia
    // incondicionalmente, tambien en la primera vinculacion (donde ya esta vacio): Apply nunca
    // ramifica por logica de negocio, solo asienta estado (MEF-ADR-0004 capa 4).
    public void Apply(VinculacionIniciada e)
    {
        _fechaTerminacionVinculacionAnterior = _fechaTerminacionVinculacionVigente;
        _codigoVinculacionVigente = e.Codigo;
        _fechaInicioVinculacionVigente = e.FechaInicio;
        _fechaTerminacionVinculacionVigente = null;
        _etiquetas.Clear();
    }

    // Issue #349: registra la terminacion de la vinculacion vigente. Nunca lanza (MEF-ADR-0004
    // capa 4).
    public void Apply(VinculacionTerminada e) => _fechaTerminacionVinculacionVigente = e.FechaEfectiva;

    // Issue #354: anula la terminacion registrada de la ULTIMA vinculacion -- la reabre sin tocar
    // su codigo ni su fecha de inicio (ambos quedan intactos, Apply solo limpia la terminacion).
    // Nunca lanza (MEF-ADR-0004 capa 4).
    public void Apply(TerminacionAnulada e) => _fechaTerminacionVinculacionVigente = null;

    // Issue #351: reemplaza el nombre de la persona. Nunca lanza (MEF-ADR-0004 capa 4).
    public void Apply(NombresCorregidos e) => _nombre = e.Nombre;

    // Issue #352: reemplaza la fecha de inicio de la ULTIMA vinculacion (tenga o no terminacion
    // registrada). Nunca lanza (MEF-ADR-0004 capa 4).
    public void Apply(FechaInicioVinculacionCorregida e) => _fechaInicioVinculacionVigente = e.FechaInicio;

    // Issue #355: registra/sobrescribe la etiqueta bajo su categoria normalizada -- un valor por
    // categoria (CA-2: "Área" sobre "area" sobrescribe, nunca duplica). Nunca lanza (MEF-ADR-0004
    // capa 4).
    public void Apply(EtiquetaAsignada e) => _etiquetas[e.Etiqueta.CategoriaNormalizada] = e.Etiqueta;

    // Issue #355: retira la etiqueta de esa categoria normalizada del diccionario. Nunca lanza
    // (MEF-ADR-0004 capa 4).
    public void Apply(EtiquetaRetirada e) => _etiquetas.Remove(e.CategoriaNormalizada);

    // Issue #349: mecanismo "declinar con resultado" (CA-ADR-0030) -- nunca lanza, nunca emite un
    // evento de fallo persistido. Issue #379 (MEF-ADR-0043 paso 4, CA-5): gana el parametro
    // codigo -- el {codigo} de la ruta HTTP, comparado contra _codigoVinculacionVigente ANTES de
    // evaluar las demas reglas (salvaguarda tipo concurrencia optimista: rechaza actuar sobre la
    // vinculacion equivocada tras un reingreso no visto por el cliente). Tres razones de rechazo
    // evaluables solo con la historia del stream, sin reloj (decision de refinamiento):
    //   - CodigoNoCorresponde (PRIMERA, #379): codigo != _codigoVinculacionVigente (comparacion
    //     exacta, case-sensitive: #387 preserva el case del codigo).
    //   - YaTerminada: _fechaTerminacionVinculacionVigente ya tiene valor (incluye un preaviso
    //     cuya fecha aun no llego -- "ya terminada" es "tiene terminacion registrada", no "la
    //     fecha ya paso").
    //   - FechaAnteriorAInicio: fechaEfectiva < _fechaInicioVinculacionVigente (duracion
    //     negativa). fechaEfectiva == _fechaInicioVinculacionVigente es valida (vinculacion de un
    //     solo dia).
    // Exito: appendea VinculacionTerminada a _uncommittedEvents y lo aplica.
    // internal, como Registrar y como los metodos de comando de los demas aggregates del repo: el
    // unico llamador es el handler del mismo ensamblado (los tests lo alcanzan via InternalsVisibleTo).
    // STUB de la fase roja del pipeline TDD (test-writer): la logica real (comparacion de codigo +
    // las dos reglas ya existentes) la reimplementa el implementer en la fase verde -- este agente
    // nunca escribe implementacion real.
    internal ResultadoTerminacionVinculacion TerminarVinculacion(string codigo, DateOnly fechaEfectiva) =>
        throw new NotImplementedException();

    // Issue #378 (MEF-ADR-0043 paso 1, absorbe #350): inicia una vinculacion nueva sobre un
    // colaborador EXISTENTE -- create disfrazado, verificado contra la historia del stream: emite
    // el MISMO evento que Registrar (VinculacionIniciada, CA-ADR-0029: un evento no conoce su
    // comando) -- mismo hecho, comando distinto. Antes se llamaba Reingresar (issue #350): el
    // rename es puramente de nombre (CA-4) -- "reingreso" sigue nombrando el escenario de negocio,
    // deja de nombrar la operacion.
    // Mecanismo "declinar con resultado" (CA-ADR-0030) -- nunca lanza, nunca emite un evento de
    // fallo persistido. Dos razones de rechazo evaluables solo con la historia del stream, sin
    // reloj (invariante de no-solape, doctrina del preaviso #349):
    //   - VinculacionAbierta: _fechaTerminacionVinculacionVigente is null (incluye un reingreso
    //     previo sin terminar).
    //   - FechaSolapaVinculacionAnterior: fechaInicio <= _fechaTerminacionVinculacionVigente.Value
    //     (estrictamente posterior es la unica fecha valida -- el mismo dia se rechaza).
    // Exito: appendea VinculacionIniciada(codigo, fechaInicio) a _uncommittedEvents y lo aplica --
    // ese Apply reabre la vinculacion (limpia _fechaTerminacionVinculacionVigente), de modo que el
    // ciclo registro-terminacion-reingreso-terminacion es encadenable sin estado residual.
    // internal: mismo criterio de visibilidad que TerminarVinculacion y Registrar.
    internal ResultadoInicioVinculacion IniciarVinculacion(string codigo, DateOnly fechaInicio)
    {
        if (_fechaTerminacionVinculacionVigente is null)
            return ResultadoInicioVinculacion.VinculacionAbierta;

        if (fechaInicio <= _fechaTerminacionVinculacionVigente.Value)
            return ResultadoInicioVinculacion.FechaSolapaVinculacionAnterior;

        var evento = new VinculacionIniciada(codigo, fechaInicio);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoInicioVinculacion.Exitosa;
    }

    // Issue #351: mecanismo "declinar en silencio" (precedente ControlDiarioAggregateRoot.
    // AdicionarMarcacion) -- nunca lanza ni emite un evento de fallo persistido, y a diferencia de
    // TerminarVinculacion/IniciarVinculacion no responde razon: sin reglas de estado que violar,
    // la unica causa de no emitir es que no haya nada que corregir, y el borde responde 202 igual.
    // La idempotencia es por igualdad de VALOR (NombreColaborador.Equals, #348), no por los
    // primitivos crudos del comando: el handler ya construyo el VO, que normaliza trim y opcionales
    // ausentes antes de que esta comparacion ocurra.
    // No mira la vigencia de la vinculacion: los nombres son de la PERSONA, no de la vinculacion
    // (decision de refinamiento 2026-08-11), asi que corregir sobre una vinculacion terminada es
    // valido. La existencia del colaborador ya la garantizo el handler al rehidratarlo.
    // internal: mismo criterio de visibilidad que TerminarVinculacion/IniciarVinculacion.
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
    // idempotencia. Issue #379 (MEF-ADR-0043 paso 4, CA-5): gana el parametro codigo -- el
    // {codigo} de la ruta HTTP, comparado contra _codigoVinculacionVigente ANTES de evaluar
    // cualquier otra regla, INCLUYENDO la idempotencia (SinCambios): un comando dirigido a la
    // vinculacion equivocada no debe filtrar informacion sobre el estado de la vigente, ni
    // siquiera "no habia nada que corregir". Cuatro reglas evaluables solo con la historia del
    // stream, sin reloj (decision de refinamiento 2026-08-11):
    //   - CodigoNoCorresponde (PRIMERA, #379): codigo != _codigoVinculacionVigente (comparacion
    //     exacta, case-sensitive: #387 preserva el case del codigo).
    //   - SinCambios (idempotencia, segunda): fechaCorregida == _fechaInicioVinculacionVigente
    //     -> ningun evento, sin excepcion (patron #351: la idempotencia no consulta las demas reglas).
    //   - FechaPosteriorATerminacionPropia: la ULTIMA vinculacion tiene terminacion registrada
    //     (_fechaTerminacionVinculacionVigente is not null) y fechaCorregida >
    //     _fechaTerminacionVinculacionVigente.Value (fechaCorregida == la propia terminacion es
    //     valida: vinculacion de un solo dia, consistente con TerminarVinculacion #349).
    //   - FechaSolapaVinculacionAnterior: no-solape hacia atras, solo ejercitable cuando existe una
    //     vinculacion anterior (tras un reingreso, #350) -- fechaCorregida es igual o anterior a la
    //     FechaEfectiva de esa vinculacion anterior (misma frontera que IniciarVinculacion #378:
    //     el dia de la fecha efectiva pertenece a la vinculacion que termino).
    // Exito: appendea FechaInicioVinculacionCorregida a _uncommittedEvents y lo aplica.
    // internal: mismo criterio de visibilidad que TerminarVinculacion/IniciarVinculacion/
    // CorregirNombres -- el unico llamador es el handler del mismo ensamblado (los tests lo
    // alcanzan via InternalsVisibleTo).
    // STUB de la fase roja del pipeline TDD (test-writer): la logica real (comparacion de codigo +
    // las tres reglas ya existentes) la reimplementa el implementer en la fase verde -- este
    // agente nunca escribe implementacion real.
    internal ResultadoCorreccionFechaInicioVinculacion CorregirFechaInicio(string codigo, DateOnly fechaCorregida) =>
        throw new NotImplementedException();

    // Issue #354: mecanismo "declinar con resultado" (CA-ADR-0030) -- nunca lanza, nunca emite un
    // evento de fallo persistido. Issue #379 (MEF-ADR-0043 paso 4, CA-5): gana el parametro
    // codigo -- el {codigo} de la ruta HTTP, comparado contra _codigoVinculacionVigente ANTES de
    // evaluar la unica regla de estado (salvaguarda tipo concurrencia optimista). Dos razones de
    // rechazo evaluables solo con la historia del stream, sin reloj (decision de refinamiento
    // 2026-08-11 -- el arrepentimiento del preaviso y la fecha de terminacion errada comparten
    // esta misma solucion):
    //   - CodigoNoCorresponde (PRIMERA, #379): codigo != _codigoVinculacionVigente (comparacion
    //     exacta, case-sensitive: #387 preserva el case del codigo).
    //   - VinculacionAbierta: _fechaTerminacionVinculacionVigente is null -- cubre tres casos que
    //     el handler no distingue entre si (recien registrada, reingresada, o ya anulada antes,
    //     CA-3/CA-4): tras un reingreso la terminacion de la vinculacion ANTERIOR queda congelada
    //     (decision aprobada explicitamente) porque solo la ULTIMA vinculacion cuenta.
    // Exito: appendea TerminacionAnulada a _uncommittedEvents y lo aplica -- reabre la vinculacion
    // vigente con su codigo y fecha de inicio intactos (Apply no los toca).
    // internal: mismo criterio de visibilidad que TerminarVinculacion/IniciarVinculacion/
    // CorregirNombres/CorregirFechaInicio -- el unico llamador es el handler del mismo ensamblado
    // (los tests lo alcanzan via InternalsVisibleTo).
    // STUB de la fase roja del pipeline TDD (test-writer): la logica real (comparacion de codigo +
    // la regla ya existente) la reimplementa el implementer en la fase verde -- este agente nunca
    // escribe implementacion real.
    internal ResultadoAnulacionTerminacion AnularTerminacion(string codigo) =>
        throw new NotImplementedException();

    // Issue #355: mecanismo combinado (CA-ADR-0030) -- "declinar con resultado" para la regla de
    // apertura estricta (decision #1 del issue: la ULTIMA vinculacion no puede tener terminacion
    // registrada, incluido un preaviso sin vencer -- las etiquetas describen la relacion laboral
    // ACTIVA) y "declinar en silencio" (precedente CorregirNombres #351 / CorregirFechaInicio #352)
    // para la idempotencia (CA-2: la etiqueta nueva es igual por valor, Etiqueta.Equals #353, a la
    // que ya existe para esa categoria).
    // Un valor por categoria (CA-2, CA-4): la clave del diccionario es SIEMPRE
    // etiqueta.CategoriaNormalizada -- asignar sobre una categoria existente sobrescribe, nunca
    // duplica.
    // Exito: appendea EtiquetaAsignada a _uncommittedEvents y lo aplica.
    // internal: mismo criterio de visibilidad que los metodos de comando hermanos -- el unico
    // llamador es el handler del mismo ensamblado (los tests lo alcanzan via InternalsVisibleTo).
    internal ResultadoAsignacionEtiqueta AsignarEtiqueta(Etiqueta etiqueta)
    {
        if (_fechaTerminacionVinculacionVigente is not null)
            return ResultadoAsignacionEtiqueta.VinculacionTerminada;

        if (_etiquetas.TryGetValue(etiqueta.CategoriaNormalizada, out var existente) &&
            etiqueta.Equals(existente))
            return ResultadoAsignacionEtiqueta.SinCambios;

        var evento = new EtiquetaAsignada(etiqueta);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoAsignacionEtiqueta.Exitosa;
    }

    // Issue #355: mecanismo "declinar con resultado" puro (CA-ADR-0030) -- retirar una categoria
    // inexistente SIEMPRE rechaza (CA-4, decision de refinamiento 2026-08-11: con categorias
    // libres, un typo como "aera" por "area" debe aflorar al instante, nunca un 202 silencioso que
    // lo esconda).
    // Recibe la categoria YA NORMALIZADA (Tell-don't-Ask: el handler la obtiene de
    // Etiqueta.NormalizarCategoria, #355 -- el aggregate nunca normaliza strings por su cuenta,
    // mismo criterio que EsMismaCategoria decide "misma categoria" dentro del VO, no en el
    // llamador).
    // internal: mismo criterio de visibilidad que los metodos de comando hermanos.
    internal ResultadoRetiroEtiqueta RetirarEtiqueta(string categoriaNormalizada)
    {
        if (_fechaTerminacionVinculacionVigente is not null)
            return ResultadoRetiroEtiqueta.VinculacionTerminada;

        if (!_etiquetas.ContainsKey(categoriaNormalizada))
            return ResultadoRetiroEtiqueta.CategoriaInexistente;

        var evento = new EtiquetaRetirada(categoriaNormalizada);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoRetiroEtiqueta.Exitosa;
    }

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
