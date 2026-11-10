using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Estado observable internal, no publico: existe para que el DSL de tests (And<>) verifique el
// estado rehidratado sin ampliar la superficie publica (Tell-don't-Ask, MEF-ADR-0012).
public partial class ColaboradorAggregateRoot : AggregateRoot
{
    private Identificacion? _identificacion;
    private NombreColaborador? _nombre;
    private string? _codigoVinculacionVigente;
    private DateOnly _fechaInicioVinculacionVigente;
    private DateOnly? _fechaTerminacionVinculacionVigente;

    // Terminacion de la vinculacion que el ultimo reingreso desplazo; sobrevive al reset de
    // Apply(VinculacionIniciada) y es el unico dato para la no-solape hacia atras de
    // CorregirFechaInicio. null si el colaborador sigue en su primera vinculacion.
    private DateOnly? _fechaTerminacionVinculacionAnterior;

    // Clave = Etiqueta.CategoriaNormalizada: un valor por categoria, asignar sobrescribe, nunca
    // duplica.
    private readonly Dictionary<string, Etiqueta> _etiquetas = new();

    // Sede de la VINCULACION, no de la persona: null en el estado inicial y tras un reingreso. Solo
    // el codigo (referencia al maestro de Sedes, CA-ADR-0029 islas), nunca su nombre ni su centro de
    // costos.
    private string? _codigoSede;

    internal IReadOnlyDictionary<string, Etiqueta> Etiquetas => _etiquetas;

    internal string? CodigoSede => _codigoSede;

    internal Identificacion Identificacion => _identificacion!;
    internal NombreColaborador Nombre => _nombre!;
    internal string CodigoVinculacionVigente => _codigoVinculacionVigente!;
    internal DateOnly FechaInicioVinculacionVigente => _fechaInicioVinculacionVigente;

    // null mientras la vinculacion esta abierta; con valor tras terminarla (registro tardio o
    // preaviso, sin distincion). El handler decide por el resultado del comando, nunca leyendo esto.
    internal DateOnly? FechaTerminacionVinculacionVigente => _fechaTerminacionVinculacionVigente;

    // Punto unico de la clave del stream (MEF-ADR-0037): ningun handler/endpoint la concatena.
    public static string ComputarStreamId(Identificacion identificacion) => identificacion.ToString();

    // Apply publicos: TestStore.ApplyEvent los encuentra via GetMethods(). Nunca lanzan
    // (MEF-ADR-0004 capa 4).
    public void Apply(ColaboradorRegistrado e)
    {
        Id = ComputarStreamId(e.Identificacion);
        _identificacion = e.Identificacion;
        _nombre = e.Nombre;
    }

    // Un reingreso nace limpio: sin la terminacion, las etiquetas, la sede ni la jornada de la
    // vinculacion anterior (sin el reset de la terminacion quedaria "ya terminado"). Se limpia sin
    // ramificar, tambien en la primera vinculacion: Apply no ramifica por logica de negocio. El
    // orden importa: la terminacion se guarda como anterior antes de resetearla.
    public void Apply(VinculacionIniciada e)
    {
        _fechaTerminacionVinculacionAnterior = _fechaTerminacionVinculacionVigente;
        _codigoVinculacionVigente = e.Codigo;
        _fechaInicioVinculacionVigente = e.FechaInicio;
        _fechaTerminacionVinculacionVigente = null;
        _etiquetas.Clear();
        _codigoSede = e.CodigoSede;
        _jornadaId = null;
    }

    public void Apply(VinculacionTerminada e) => _fechaTerminacionVinculacionVigente = e.FechaEfectiva;

    public void Apply(TerminacionAnulada e) => _fechaTerminacionVinculacionVigente = null;

    public void Apply(NombresCorregidos e) => _nombre = e.Nombre;

    public void Apply(FechaInicioVinculacionCorregida e) => _fechaInicioVinculacionVigente = e.FechaInicio;

    public void Apply(EtiquetaAsignada e) => _etiquetas[e.Etiqueta.CategoriaNormalizada] = e.Etiqueta;

    public void Apply(EtiquetaRetirada e) => _etiquetas.Remove(e.CategoriaNormalizada);

    public void Apply(SedeAsignada e) => _codigoSede = e.CodigoSede;

    // Declina con resultado (CA-ADR-0030), sin reloj: solo con la historia del stream.
    // El codigo se compara PRIMERO y case-sensitive: salvaguarda tipo concurrencia optimista contra
    // actuar sobre la vinculacion equivocada tras un reingreso que el cliente no vio.
    // "Ya terminada" es "tiene terminacion registrada", incluido un preaviso que aun no llega.
    // fechaEfectiva == inicio es valida (vinculacion de un solo dia).
    internal ResultadoTerminacionVinculacion TerminarVinculacion(string codigo, DateOnly fechaEfectiva)
    {
        if (codigo != _codigoVinculacionVigente)
            return ResultadoTerminacionVinculacion.CodigoNoCorresponde;

        if (_fechaTerminacionVinculacionVigente is not null)
            return ResultadoTerminacionVinculacion.YaTerminada;

        if (fechaEfectiva < _fechaInicioVinculacionVigente)
            return ResultadoTerminacionVinculacion.FechaAnteriorAInicio;

        var evento = new VinculacionTerminada(fechaEfectiva);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoTerminacionVinculacion.Exitosa;
    }

    // El reingreso: emite el MISMO evento que Registrar (un evento no conoce su comando,
    // CA-ADR-0029). Declina con resultado (CA-ADR-0030), sin reloj. La fecha debe ser
    // estrictamente posterior a la terminacion anterior: el mismo dia se rechaza.
    internal ResultadoInicioVinculacion IniciarVinculacion(string codigo, DateOnly fechaInicio, string? codigoSede)
    {
        if (_fechaTerminacionVinculacionVigente is null)
            return ResultadoInicioVinculacion.VinculacionAbierta;

        if (fechaInicio <= _fechaTerminacionVinculacionVigente.Value)
            return ResultadoInicioVinculacion.FechaSolapaVinculacionAnterior;

        var evento = new VinculacionIniciada(codigo, fechaInicio, codigoSede);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoInicioVinculacion.Exitosa;
    }

    // Declina en silencio, sin razon: la unica causa de no emitir es que no haya nada que corregir,
    // y el borde responde 204 igual. Idempotencia por igualdad de VALOR del VO ya normalizado.
    // No mira la vigencia: los nombres son de la PERSONA, no de la vinculacion.
    internal void CorregirNombres(NombreColaborador nombre)
    {
        if (nombre.Equals(_nombre))
            return;

        var evento = new NombresCorregidos(nombre);
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    // Declina con resultado (CA-ADR-0030), sin reloj. El orden importa: el codigo se compara
    // ANTES que la idempotencia, para no filtrar a un comando dirigido a la vinculacion equivocada
    // ni siquiera "no habia nada que corregir". Igual a la propia terminacion es valido (un solo
    // dia); el dia de la terminacion anterior pertenece a la vinculacion que termino.
    internal ResultadoCorreccionFechaInicioVinculacion CorregirFechaInicio(string codigo, DateOnly fechaCorregida)
    {
        if (codigo != _codigoVinculacionVigente)
            return ResultadoCorreccionFechaInicioVinculacion.CodigoNoCorresponde;

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

    // Cubre el arrepentimiento del preaviso y la fecha de terminacion errada (anular + terminar de
    // nuevo). Declina con resultado (CA-ADR-0030), sin reloj, codigo primero. Tras un reingreso la
    // terminacion de la vinculacion ANTERIOR queda congelada (decision explicita): solo cuenta la
    // ULTIMA.
    internal ResultadoAnulacionTerminacion AnularTerminacion(string codigo)
    {
        if (codigo != _codigoVinculacionVigente)
            return ResultadoAnulacionTerminacion.CodigoNoCorresponde;

        if (_fechaTerminacionVinculacionVigente is null)
            return ResultadoAnulacionTerminacion.VinculacionAbierta;

        var evento = new TerminacionAnulada();
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoAnulacionTerminacion.Exitosa;
    }

    // Un preaviso sin vencer tambien rechaza: las etiquetas describen la relacion laboral ACTIVA.
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

    // Recibe la categoria YA NORMALIZADA (Etiqueta.NormalizarCategoria): el aggregate nunca
    // normaliza strings. Retirar una categoria ausente es exito sin evento, nunca un rechazo.
    internal ResultadoRetiroEtiqueta RetirarEtiqueta(string categoriaNormalizada)
    {
        if (_fechaTerminacionVinculacionVigente is not null)
            return ResultadoRetiroEtiqueta.VinculacionTerminada;

        if (!_etiquetas.ContainsKey(categoriaNormalizada))
            return ResultadoRetiroEtiqueta.SinCambios;

        var evento = new EtiquetaRetirada(categoriaNormalizada);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoRetiroEtiqueta.Exitosa;
    }

    // Mecanismo combinado (CA-ADR-0030): rechaza si la vinculacion tiene terminacion registrada --
    // un preaviso sin vencer bloquea igual, la sede describe la relacion laboral ACTIVA -- y declina
    // en silencio si el codigo es identico (comparacion exacta, case-sensitive: dos codigos que solo
    // difieren en mayusculas son sedes distintas para el maestro).
    internal ResultadoAsignacionSede AsignarSede(string codigoSede)
    {
        if (_fechaTerminacionVinculacionVigente is not null)
            return ResultadoAsignacionSede.VinculacionTerminada;

        if (codigoSede == _codigoSede)
            return ResultadoAsignacionSede.SinCambios;

        var evento = new SedeAsignada(codigoSede);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoAsignacionSede.Exitosa;
    }

    // Los DOS eventos van en el mismo commit.
    internal static ColaboradorAggregateRoot Registrar(
        Identificacion identificacion, NombreColaborador nombre, string codigo, DateOnly fechaInicio,
        string? codigoSede)
    {
        var colaborador = new ColaboradorAggregateRoot();

        var colaboradorRegistrado = new ColaboradorRegistrado(identificacion, nombre);
        colaborador._uncommittedEvents.Add(colaboradorRegistrado);
        colaborador.Apply(colaboradorRegistrado);

        var vinculacionIniciada = new VinculacionIniciada(codigo, fechaInicio, codigoSede);
        colaborador._uncommittedEvents.Add(vinculacionIniciada);
        colaborador.Apply(vinculacionIniciada);

        return colaborador;
    }
}
