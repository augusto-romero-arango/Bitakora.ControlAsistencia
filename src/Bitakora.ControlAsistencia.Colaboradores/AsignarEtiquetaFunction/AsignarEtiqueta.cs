namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;

// Issue #355: comando para asignar (o sobrescribir) una etiqueta dinamica -- par categoria:valor
// libre, sin catalogo previo -- a la vinculacion vigente de un colaborador. Septimo comando del
// ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357).
// Trigger: HTTP POST, Route: Colaboradores/Etiquetas (identificacion en el body porque su
// representacion "CC:79543210" contiene ":", hostil como segmento de URL -- mismo criterio que el
// resto del ciclo de vida).
// Payload primitivo -- igual que los demas comandos del ciclo de vida (MEF-ADR-0039 decision 6,
// payload por rol): NUNCA reusa un tipo de Colaboradores.DomainEvents como campo. El handler
// construye Etiqueta a partir de Categoria/Valor (parseo tipado unico en el borde, MEF-ADR-0037
// seccion 2, mismo criterio que Identificacion).
public record AsignarEtiqueta(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Categoria,
    string Valor);
