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
| Value Object con invariantes | `sealed class` con factory static | Privado parametrizado + privado vacío | Factory static → throw |
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
// Value Object con invariantes — sealed class, no record
public sealed class Cedula : IEquatable<Cedula>
{
    // Estado interno — nadie lo ve desde afuera
    private readonly string _numero;

    // Constructor real: usado solo por el factory
    private Cedula(string numero) => _numero = numero;

    // Constructor vacio: SOLO para STJ/Marten — nunca lo usa el dominio
    private Cedula() => _numero = string.Empty;

    // Unica entrada publica: valida y construye
    public static Cedula Crear(string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            throw new ArgumentException("La cedula no puede estar vacia");
        return new Cedula(numero);
    }

    // Comportamiento publico: Tell Don't Ask
    public bool EsValida() => !string.IsNullOrWhiteSpace(_numero);
    public override string ToString() => _numero;

    // Igualdad por valor (lo que record daba gratis)
    public bool Equals(Cedula? other) => other is not null && _numero == other._numero;
    public override bool Equals(object? obj) => Equals(obj as Cedula);
    public override int GetHashCode() => _numero.GetHashCode();

    // Mapping de serializacion — vive aqui porque cambia con la clase
    internal static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var fNumero = typeof(Cedula)
            .GetField("_numero", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ctor = typeof(Cedula)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(Cedula)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (Cedula)ctor.Invoke(null);

            var pNumero = typeInfo.CreateJsonPropertyInfo(typeof(string), "numero");
            pNumero.Get = obj => fNumero.GetValue(obj)!;
            pNumero.Set = (obj, val) => fNumero.SetValue(obj, val);
            typeInfo.Properties.Add(pNumero);
        });
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

### Serialización sin atributos en la clase de dominio

Los value objects con campos privados necesitan que el serializador (STJ/Marten) pueda
leer y escribir esos campos sin que la clase exponga propiedades públicas. La solución
es el **Contract Model de STJ** (desde .NET 7), que permite configurar la serialización
externamente — el mismo principio que el Fluent API de EF Core para persistencia.

**El mapping vive en la misma clase** porque cambia junto con ella:

```csharp
internal static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
{
    // Capturar FieldInfo una sola vez (se cachea en la clase estatica)
    var fCampo = typeof(MiValueObject)
        .GetField("_campo", BindingFlags.NonPublic | BindingFlags.Instance)!;
    var ctor = typeof(MiValueObject)
        .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

    resolver.Modifiers.Add(typeInfo =>
    {
        if (typeInfo.Type != typeof(MiValueObject)) return;
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        // STJ usa el constructor vacio privado para crear la instancia
        typeInfo.CreateObject = () => (MiValueObject)ctor.Invoke(null);

        // Cada campo privado se registra como propiedad JSON
        var p = typeInfo.CreateJsonPropertyInfo(typeof(int), "campo");
        p.Get = obj => fCampo.GetValue(obj)!;
        p.Set = (obj, val) => fCampo.SetValue(obj, val); // bypasea readonly via reflexion
        typeInfo.Properties.Add(p);
    });
}
```

**Registro en infraestructura** — cada dominio llama a sus mappings al inicializar Marten:

```csharp
// En MartenEventStoreExtensions o en el Program.cs del dominio
options.UseSystemTextJsonForSerialization(configure: jsonOptions =>
{
    var resolver = new DefaultJsonTypeInfoResolver();
    MiValueObject.ConfigurarSerializacion(resolver);
    OtroValueObject.ConfigurarSerializacion(resolver);
    jsonOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(resolver,
        jsonOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver());
});
```

**Riesgo documentado:** `FieldInfo.SetValue` en campos `readonly` funciona en .NET 10
con el runtime JIT. No es compatible con NativeAOT/trimming. Este proyecto usa Azure
Functions isolated worker sin NativeAOT, por lo que el riesgo no aplica actualmente.

### Otras heurísticas transversales

- `{ get; init; }` prohibido en objetos con invariantes — `with {}` bypasea la validación del factory
- Constructor vacío siempre `private`, nunca `protected` ni `public`
- `record` solo para tipos sin invariantes (comandos, eventos simples, value objects planos)
- `sealed class` con `IEquatable<T>` para value objects con invariantes — no `record`
- `public get; private set;` para propiedades mutables en clases (aggregates)
- Usar `public get;` (sin setter explícito) en records que no tienen factory

## Consecuencias

**Positivas**

- Objetos con invariantes son self-validating desde su construcción
- Encapsulamiento real: el estado no puede corromperse desde afuera
- Consistencia en el proyecto: cualquier persona puede saber qué esperar de un record vs clase
- Los eventos con precondiciones documentan sus contratos en el código

**Negativas**

- Más tipos de factory static que constructores simples
- `IEquatable<T>` hay que implementarlo manualmente (lo que `record` daba gratis)
- El método `ConfigurarSerializacion` en cada value object es boilerplate obligatorio
- El mapping usa reflexión — incompatible con NativeAOT (riesgo aceptado, ver sección de serialización)
- Aggregates que acumulan estado de múltiples fuentes pueden crecer — señal de revisar
  los boundaries del dominio en la fase de descubrimiento

## Referencias

- ADR-0007: Manejo de errores — aggregate nunca throw para logica de negocio
- ADR-0002: Contracts — records de eventos y value objects compartidos
- Vaughn Vernon — "Implementing Domain-Driven Design", Value Objects
