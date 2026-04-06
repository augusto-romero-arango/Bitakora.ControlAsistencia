# ADR-0015: Heuristicas de modelado de objetos de dominio

## Estado

Aceptado

## Contexto

El proyecto usa event sourcing con multiples tipos de objetos: aggregates, eventos, comandos,
value objects. Sin heuristicas explicitas, cada desarrollador toma decisiones distintas sobre
cuando usar records vs clases, cuando aplicar factory statics, y como honrar el encapsulamiento.

Estas heuristicas no son principios absolutos. En desarrollo de software nada es blanco o negro.
El diseno especifico de cada caso puede ajustarse durante la fase de descubrimiento con
event-stormer o planner. Lo que aqui se define es el punto de partida -- la decision por defecto
en ausencia de un diseno especifico.

## Decision

### Tabla de heuristicas por tipo de objeto

| Tipo de objeto | Forma | Constructor | Validacion |
|---|---|---|---|
| Comando (DTO) | `record` con constructor primario | Publico | FluentValidation (externa) |
| Evento sin invariantes | `record` con constructor primario | Publico | Ninguna |
| Evento con precondiciones | `sealed class` con factory static | Privado parametrizado + privado vacio | Factory static -> throw |
| Value Object simple | `record` con constructor primario | Publico | Ninguna |
| Value Object con invariantes | `sealed class` con factory static | Privado parametrizado + privado vacio | Factory static -> throw |
| AggregateRoot | `partial class` | Factory static (`Crear`) | Eventos de fallo (ADR-0007) |

La distincion entre record y clase se basa en **invariantes**, no solo en mutabilidad:
- Si el objeto no tiene invariantes de construccion -> `record` (comandos, eventos simples, VOs planos)
- Si el objeto tiene invariantes que debe proteger -> `sealed class` con factory static (eventos con precondiciones, VOs complejos)
- Si el objeto tiene estado que cambia a lo largo de su vida -> `class` (aggregates)

**Nota sobre inmutabilidad**: los eventos son inmutables por convencion de diseno, no por
imposicion del sistema de tipos. Un `sealed class` evento no impide programaticamente la
mutacion, pero la heuristica del equipo es clara: los eventos representan hechos del pasado
y no deben modificarse. Si alguien necesita mutar un evento, la conversacion correcta es
sobre diseno, no sobre como saltarse la proteccion.

**Nota sobre equality**: los eventos no necesitan `IEquatable<T>` porque se identifican por
su posicion en el event stream, no por igualdad estructural. Usar `record` para eventos con
colecciones (ej: `IReadOnlyList<T>`) promete igualdad por valor que no cumple -- `sealed class`
no hace esa promesa.

### Factory static para objetos con invariantes

Cuando un objeto tiene reglas que deben cumplirse en la construccion, se usa el patron
factory static:

1. Constructor primario privado (recibe solo los campos, sin validar)
2. Constructor vacio privado para compatibilidad con persistencia y serializacion
3. Metodo estatico publico `Crear(...)` que valida y construye
4. Validaciones como metodos privados estaticos del mismo objeto

```csharp
// Value Object con invariantes -- sealed class, no record
public sealed class Cedula : IEquatable<Cedula>
{
    // Estado interno -- nadie lo ve desde afuera
    private readonly string _numero;

    // Constructor real: usado solo por el factory
    private Cedula(string numero) => _numero = numero;

    // Constructor vacio: SOLO para STJ/Marten -- nunca lo usa el dominio
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

    // Mapping de serializacion -- vive aqui porque cambia con la clase
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

Un evento puede tener precondiciones estructurales que deben cumplirse para que sea valido
(campos vacios, GUIDs vacios, rangos invalidos). En ese caso, el evento usa `sealed class`
con factory static -- el mismo patron que los value objects con invariantes, pero sin
`IEquatable<T>` (los eventos no se comparan por valor).

El CommandHandler construye el evento antes de pasarlo al aggregate. Si la construccion falla,
el throw ocurre en el handler -- no en el aggregate (ADR-0007 se mantiene).

```csharp
// Evento con precondiciones - sealed class, no record
// No implementa IEquatable porque los eventos se identifican por posicion en el stream
public sealed class TurnoAsignado
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

    // Constructor vacio para STJ/Marten
    private TurnoAsignado()
    {
        TurnoId = Guid.Empty;
        EmpleadoId = Guid.Empty;
        FechaInicio = DateOnly.MinValue;
    }

    public static TurnoAsignado Crear(Guid turnoId, Guid empleadoId, DateOnly fechaInicio)
    {
        if (turnoId == Guid.Empty)
            throw new ArgumentException("El turno es requerido");
        if (empleadoId == Guid.Empty)
            throw new ArgumentException("El empleado es requerido");
        return new TurnoAsignado(turnoId, empleadoId, fechaInicio);
    }

    // ConfigurarSerializacion si tiene campos privados que STJ no puede ver
    // (en este ejemplo las propiedades son publicas get, asi que STJ las lee directamente;
    // el constructor vacio + reflexion en Set resuelve la deserializacion)
}
```

### Encapsulamiento: Tell Don't Ask

Los calculos pertenecen al objeto que tiene los datos necesarios. Si un metodo de otra clase
necesita datos del objeto para calcular algo, ese calculo debe vivir dentro del objeto.

En event sourcing, la heuristica es **preferir el aggregate que acumula estado via eventos**
sobre un servicio externo que jala datos. Un aggregate puede escuchar eventos de multiples
dominios y ejecutar calculos con toda la informacion que necesita.

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

Si un calculo genuinamente cruza multiples aggregates de formas que no pueden resolverse
con acumulacion de eventos, la alternativa es una proyeccion (read model) o un process
manager -- no un domain service que rompa el encapsulamiento. Este diseno se decide en la
fase de descubrimiento, no como default.

### Serializacion sin atributos en la clase de dominio

Los value objects y eventos con campos privados necesitan que el serializador (STJ/Marten)
pueda leer y escribir esos campos sin que la clase exponga propiedades publicas. La solucion
es el **Contract Model de STJ** (desde .NET 7), que permite configurar la serializacion
externamente -- el mismo principio que el Fluent API de EF Core para persistencia.

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

**Nota sobre eventos con propiedades publicas get-only**: si el evento expone propiedades
publicas con `{ get; }` (no campos privados), STJ puede serializarlas directamente sin
necesidad de `ConfigurarSerializacion`. Solo se necesita el mapping cuando hay campos
privados que STJ no puede ver. El constructor vacio privado es necesario en ambos casos
para la deserializacion.

**Registro en infraestructura** -- cada dominio llama a sus mappings al inicializar Marten:

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

### Otras heuristicas transversales

- `{ get; init; }` prohibido en objetos con invariantes -- `with {}` bypasea la validacion del factory
- Constructor vacio siempre `private`, nunca `protected` ni `public`
- `record` solo para tipos sin invariantes (comandos, eventos simples, value objects planos)
- `sealed class` para objetos con invariantes -- value objects (con `IEquatable<T>`) y eventos con precondiciones (sin `IEquatable<T>`)
- `public get; private set;` para propiedades mutables en clases (aggregates)
- Usar `public get;` (sin setter explicito) en records que no tienen factory

## Consecuencias

**Positivas**

- Objetos con invariantes son self-validating desde su construccion
- Encapsulamiento real: el estado no puede corromperse desde afuera
- Consistencia en el proyecto: cualquier persona puede saber que esperar de un record vs clase
- Los eventos con precondiciones documentan sus contratos en el codigo
- `sealed class` para eventos con colecciones no promete equality que no cumple

**Negativas**

- Mas tipos de factory static que constructores simples
- `IEquatable<T>` hay que implementarlo manualmente en value objects (lo que `record` daba gratis)
- El metodo `ConfigurarSerializacion` en cada value object con campos privados es boilerplate obligatorio
- El mapping usa reflexion -- incompatible con NativeAOT (riesgo aceptado, ver seccion de serializacion)
- Aggregates que acumulan estado de multiples fuentes pueden crecer -- senal de revisar
  los boundaries del dominio en la fase de descubrimiento

## Referencias

- ADR-0007: Manejo de errores -- aggregate nunca throw para logica de negocio
- ADR-0002: Contracts -- records de eventos y value objects compartidos
- Vaughn Vernon -- "Implementing Domain-Driven Design", Value Objects
