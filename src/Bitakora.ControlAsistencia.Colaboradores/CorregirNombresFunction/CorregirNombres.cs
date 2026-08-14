namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;

// Issue #351: comando para corregir los nombres de un colaborador existente. Cuarto comando del
// ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357) y el mas simple: sin reglas de
// estado -- solo exige existencia del colaborador, nunca vigencia de su vinculacion (los nombres
// son de la PERSONA, no de la vinculacion, decision de refinamiento 2026-08-11).
// Trigger: HTTP POST, Route: Colaboradores/Nombres (el recurso que se reemplaza; la identificacion
// viaja en el body, decision vigente hasta #378 -- rutas orientadas a recurso --, mismo criterio
// que TerminarVinculacion/ReingresarColaborador: el issue #381 cambio la representacion a
// "CC-79543210" justamente para que la llave sea apta como segmento de URI).
// Payload primitivo -- igual que RegistrarColaborador/TerminarVinculacion (MEF-ADR-0039 decision 6,
// payload por rol): NUNCA reusa un tipo de Colaboradores.DomainEvents como campo. El handler
// construye TipoIdentificacion/Identificacion/NombreColaborador a partir de estos primitivos
// (parseo tipado unico en el borde, MEF-ADR-0037 seccion 2).
public record CorregirNombres(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido);
