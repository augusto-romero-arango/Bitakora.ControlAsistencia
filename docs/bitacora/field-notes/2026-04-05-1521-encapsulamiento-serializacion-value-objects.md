# Field Note — 2026-04-05
## Encapsulamiento fuerte + serialización sin atributos para Value Objects

### Tipo de sesión
Investigación / decisiones arquitectónicas — pipeline TDD paso a paso del issue #2

---

### Contexto

Sesión de ejecución manual del pipeline TDD para el issue #2 (value objects de franja temporal).
Al llegar al reviewer, surgió una tensión de diseño: el implementer dejó propiedades `public` en
los value objects argumentando que otros dominios necesitan acceder a ellas para serialización.

El usuario rechazó ese argumento con el principio **Tell Don't Ask**: si alguien externo necesita
algo del objeto, debe preguntarle vía métodos — no que el objeto exponga sus internals. Esto
desencadenó una investigación profunda sobre cómo maximizar el encapsulamiento sin sacrificar
la capacidad de Marten/STJ de serializar los objetos.

---

### Descubrimientos

#### 1. El gap del pipeline: el test-writer adivina la interfaz pública

Cuando el issue no define explícitamente qué es público, el test-writer tiende a crear stubs
con propiedades públicas innecesarias. El error no está en los tests (que usaban `ToString()`
correctamente) sino en los **stubs**, que definieron una API más amplia de lo necesario.

**Decisión**: el planner debe incluir una sección "Interfaz pública" para value objects complejos.
El test-writer la usa como contrato vinculante — solo puede poner `public` lo que allí aparece.

#### 2. `record` no es la forma correcta para VOs con invariantes

Con todos los datos privados, el `record` no aporta nada:
- `ToString()` sintetizado devuelve `FranjaDescanso { }` — completamente vacío
- `with { }` queda paralizado — no hay miembros públicos que modificar
- `init` no existe para fields — solo para property accessors

La regla de Microsoft: *usa `record` cuando tienes datos de solo lectura accesibles públicamente;
usa `class` cuando encapsulas estado privado con comportamiento.*

**Decisión**: `sealed class` + `IEquatable<T>` para value objects con invariantes. `record` solo
para tipos sin invariantes (comandos, eventos simples, value objects planos).

#### 3. El equivalente del Fluent API de EF Core para serialización

`[JsonInclude]` y `[JsonConstructor]` funcionan, pero ensucian la clase de dominio con detalles
de infraestructura. La alternativa correcta es el **Contract Model de STJ** (desde .NET 7):

```csharp
new DefaultJsonTypeInfoResolver
{
    Modifiers = { miModifier }
}
```

Mismo principio que EF Core: la configuración de persistencia vive en infraestructura, no en
la entidad. El dominio no sabe que existe un serializador.

**Verificado en .NET 10**: `FieldInfo.SetValue` sobre campos `private readonly` de instancia
funciona. La restricción `readonly` aplica en tiempo de compilación, no en reflexión de instancia.

#### 4. `init` no existe en fields — solo en property accessors

El usuario preguntó si con fields el factory podría usar `new() { campo = valor }`. No puede:
C# no tiene `init` para fields. El factory con fields privados requiere el constructor
parametrizado, y el serializer necesita un constructor vacío separado. Dos constructores privados
con propósitos distintos.

#### 5. Serialización como parte del contrato del value object

El mapping de serialización cambia cada vez que cambia el value object. Por lo tanto:

**El método `ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)` vive en la misma
clase**, como método `internal static`. No en un archivo aparte, no en infraestructura.

La infraestructura solo orquesta: llama a cada `ConfigurarSerializacion` al inicializar Marten.

---

### Decisiones tomadas

1. **`sealed class` reemplaza a `record`** para value objects con invariantes (ADR-0015)
2. **Patrón dual-constructor**: privado parametrizado (dominio) + privado vacío (STJ/Marten)
3. **Sin atributos en la clase de dominio**: todo el mapping vía Contract Model en el mismo archivo
4. **`ConfigurarSerializacion` es obligatorio** en toda `sealed class` con factory static
5. **Sección "Interfaz pública"** en issues de VOs complejos — contrato vinculante para test-writer

---

### Artefactos generados

- `docs/adr/0015-estilo-modelado-objetos-dominio.md` — actualizado: tabla, patrón canónico nuevo,
  sección completa de serialización sin atributos, consecuencias negativas documentadas
- `.claude/agents/implementer.md` — reglas 14 y 15: sealed class + ConfigurarSerializacion
- `.claude/agents/planner.md` — sección "Interfaz pública" en template de issues de dominio
- `.claude/agents/test-writer.md` — sección 6c ampliada + regla 12: respetar Interfaz pública
- `memory/feedback_interfaz_publica_planner.md` — memoria persistente del aprendizaje

---

### Tensiones no resueltas / preguntas abiertas

- **¿Cuándo registrar los mappings en Marten?** `MartenEventStoreExtensions` necesita un
  parámetro `Action<JsonSerializerOptions>?` para que cada dominio inyecte sus mappings.
  Pendiente de implementar en `Cosmos.BuildingBlocks`.

- **AOT/NativeAOT**: `FieldInfo.SetValue` en `readonly` de instancia funciona en JIT pero no
  en NativeAOT. Riesgo documentado y aceptado — Azure Functions isolated worker no usa NativeAOT.

- **El refactor del worktree**: los value objects del issue #2 todavía son `record`. Quedan
  pendientes de convertir a `sealed class` con el nuevo patrón como parte del ciclo del reviewer.

- **IEquatable<T>**: ¿implementarlo manualmente o hay un helper en el proyecto? No se investigó.
  Por ahora se implementa a mano.

---

### Fuentes verificadas

- Microsoft Learn: [Custom JSON contract serialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/custom-contracts)
- Microsoft Learn: `[JsonConstructor]` soporta constructores no públicos desde .NET 8
- Marten docs: "the only support for private properties is to mark the field using `[JsonInclude]`" (razón por la que preferimos Contract Model)
- Verificado en .NET 10: `FieldInfo.SetValue` en campos `private readonly` de instancia funciona
