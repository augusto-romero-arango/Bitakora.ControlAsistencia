# ADR-0015: Heurísticas de modelado de objetos de dominio

## Estado

Aceptado

## Contexto

El proyecto usa event sourcing con múltiples tipos de objetos: aggregates, eventos, comandos,
value objects. Sin heurísticas explícitas, cada desarrollador toma decisiones distintas sobre
cuándo usar records vs clases, cuándo aplicar factory statics, y cómo honrar el encapsulamiento.

Estas heurísticas no son principios absolutos. En desarrollo de software nada es blanco o negro.
El diseño específico de cada caso puede ajustarse durante la fase de descubrimiento con
event-stormer o planner. Lo que aquí se define es el punto de partida — la decisión por defecto
en ausencia de un diseño específico.

## Decisión

### Tabla de heurísticas por tipo de objeto

| Tipo de objeto | Forma | Constructor | Validación |
|---|---|---|---|
| Comando (DTO) | `record` con constructor primario | Público | FluentValidation (externa) |
| Evento sin invariantes | `record` con constructor primario | Público | Ninguna |
| Evento con precondiciones | `record` con factory static | Privado | Factory static → throw |
| Value Object simple | `record` con constructor primario | Público | Ninguna |
| Value Object con invariantes | `record` con factory static | Privado + privado vacío | Factory static → throw |
| AggregateRoot | `partial class` | Factory static (`Crear`) | Eventos de fallo (ADR-0007) |

La distinción entre record y clase se basa en **mutabilidad**:
- Si el objeto no muta después de crearse → `record`
- Si el objeto tiene estado que cambia a lo largo de su vida → `class`

### Factory static para objetos con invariantes

Cuando un objeto tiene reglas que deben cumplirse en la construcción, se usa el patrón
factory static:

1. Constructor primario privado (recibe solo los campos, sin validar)
2. Constructor vacío privado para compatibilidad con persistencia y serialización
3. Método estático público `Crear(...)` que valida y construye
4. Validaciones como métodos privados estáticos del mismo objeto

```csharp
// Value Object con invariantes
public record Cedula
{
    public string Numero { get; }

    private Cedula(string numero) => Numero = numero;

    private Cedula() { } // para Marten y JSON

    public static Cedula Crear(string numero)
    {
        ValidarFormato(numero);
        return new Cedula(numero);
    }

    private static void ValidarFormato(string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            throw new ArgumentException("La cedula no puede estar vacia");
    }
}
```

### Eventos con precondiciones

Un evento puede tener precondiciones estructurales que deben cumplirse para que sea válido
(campos vacíos, GUIDs vacíos, rangos inválidos). En ese caso, el evento usa factory static
y el CommandHandler lo construye antes de pasarlo al aggregate. Si la construcción falla,
el throw ocurre en el handler — no en el aggregate (ADR-0007 se mantiene).

```csharp
// Evento con precondiciones - el handler lo construye
public record TurnoAsignado
{
    public Guid TurnoId { get; }
    public Guid EmpleadoId { get; }
    public DateOnly FechaInicio { get; }

    private TurnoAsignado(Guid turnoId, Guid empleadoId, DateOnly fechaInicio)
    {
        TurnoId = turnoId;
        EmpleadoId = empleadoId;
        FechaInicio = fechaInicio;
    }

    public static TurnoAsignado Crear(Guid turnoId, Guid empleadoId, DateOnly fechaInicio)
    {
        if (turnoId == Guid.Empty)
            throw new ArgumentException("El turno es requerido");
        if (empleadoId == Guid.Empty)
            throw new ArgumentException("El empleado es requerido");
        return new TurnoAsignado(turnoId, empleadoId, fechaInicio);
    }
}
```

### Encapsulamiento: Tell Don't Ask

Los cálculos pertenecen al objeto que tiene los datos necesarios. Si un método de otra clase
necesita datos del objeto para calcular algo, ese cálculo debe vivir dentro del objeto.

En event sourcing, la heurística es **preferir el aggregate que acumula estado vía eventos**
sobre un servicio externo que jala datos. Un aggregate puede escuchar eventos de múltiples
dominios y ejecutar cálculos con toda la información que necesita.

```csharp
// Preferir esto (aggregate que tiene los datos)
public void Apply(MarcacionesRecibidas e)
{
    _marcaciones.AddRange(e.Marcaciones);
    _horasDesglosadas = DesglosaHoras(); // calculo interno
}

// Evitar esto (servicio externo que accede a datos del objeto)
var horas = calculadoraHoras.Calcular(aggregate.Marcaciones, aggregate.Turno);
```

Si un cálculo genuinamente cruza múltiples aggregates de formas que no pueden resolverse
con acumulación de eventos, la alternativa es una proyección (read model) o un process
manager — no un domain service que rompa el encapsulamiento. Este diseño se decide en la
fase de descubrimiento, no como default.

### Otras heurísticas transversales

- `{ get; init; }` prohibido en objetos con invariantes — `with {}` bypasea la validación del factory
- Constructor vacío siempre `private`, nunca `protected` ni `public`
- `public get; private set;` para propiedades mutables en clases
- Usar `public get;` (sin setter explícito) en records que no tienen factory

## Consecuencias

**Positivas**

- Objetos con invariantes son self-validating desde su construcción
- Encapsulamiento real: el estado no puede corromperse desde afuera
- Consistencia en el proyecto: cualquier persona puede saber qué esperar de un record vs clase
- Los eventos con precondiciones documentan sus contratos en el código

**Negativas**

- Más tipos de factory static que constructores simples
- Aggregates que acumulan estado de múltiples fuentes pueden crecer — señal de revisar
  los boundaries del dominio en la fase de descubrimiento

## Referencias

- ADR-0007: Manejo de errores — aggregate nunca throw para logica de negocio
- ADR-0002: Contracts — records de eventos y value objects compartidos
- Vaughn Vernon — "Implementing Domain-Driven Design", Value Objects
