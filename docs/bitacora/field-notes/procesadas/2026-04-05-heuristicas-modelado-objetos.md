# Field Note — 2026-04-05
## Heurísticas de modelado de objetos de dominio

### Tipo de sesión
Diseño / decisiones arquitectónicas

---

### Contexto

Sesión para definir reglas explícitas de modelado OOP que guíen a los agentes `implementer`
y `reviewer`. El proyecto tenía buenos patrones para AggregateRoot y CommandHandler, pero no
había guías para Value Objects, eventos con invariantes, ni el estilo general de encapsulamiento.

---

### Decisiones tomadas

#### 1. Mutabilidad decide record vs clase

- No muta después de crearse → `record`
- Muta (tiene estado que cambia) → `class`
- AggregateRoot siempre es `partial class` (muta, además necesita partial para Mensajes)

Esto es más preciso que la intuición inicial de "si tiene reglas → clase". Un Value Object
puede tener validaciones y seguir siendo un record inmutable.

#### 2. Factory static para objetos con invariantes

Patrón acordado:
- Constructor primario `private` (recibe campos, no valida)
- Constructor vacío `private` para persistencia/serialización (Marten, JSON)
- Método estático `Crear(...)` que valida y construye — throw si inválido
- Validaciones como métodos privados estáticos del mismo objeto

El constructor vacío es siempre `private`, nunca `public` ni `protected`.

#### 3. Throw en factory vs ADR-0007 — la distinción clave

Se clarificó dónde vive el throw:
- Value Objects con invariantes: factory static → throw ✓
- Eventos con precondiciones estructurales: factory static → throw ✓
- AggregateRoot: nunca throw para lógica de negocio → evento de fallo ✓ (ADR-0007)

El punto interesante: los **eventos** pueden tener factory statics que throw. El
**CommandHandler** construye el evento (puede lanzar, está permitido). El aggregate
recibe el evento ya construido y aplica sus reglas de negocio emitiendo éxito o fallo.

Esto crea una separación limpia:
- Integridad estructural del evento → factory static del evento (throw)
- Reglas de negocio del dominio → aggregate (evento de fallo)

#### 4. `{ get; init; }` prohibido en objetos con invariantes

`with {}` en C# bypasea completamente el factory static. Si un objeto tiene invariantes y
usa `init`, toda la validación puede evadirse. Solución: usar `{ get; }` en records o
`{ get; private set; }` en clases.

#### 5. Encapsulamiento: Tell Don't Ask

Los cálculos pertenecen al objeto que tiene los datos. Crítica importante sobre los
Domain Services:

En esta arquitectura event-driven (sin llamadas directas entre funciones), un domain service
que "jala" datos de otros aggregates **no tiene cómo existir** sin romper la autonomía de las
funciones. La alternativa natural es el aggregate que acumula estado vía eventos.

Ejemplo discutido: `InformacionDia` que escucha `TurnoAsignado` y `MarcacionesRecibidas`,
y ejecuta internamente el desglose de horas. Esto es esencialmente un Process Manager —
exactamente lo que event sourcing habilita.

**Críticas que surgieron:**
- Riesgo de aggregate "aspiradora" que escucha demasiados eventos
- Acoplamiento disfrazado por contratos de evento
- Estado parcial si los eventos llegan desordenados
- ¿Aggregate o proyección? Si no hay invariantes que proteger, quizás es un read model

**Resolución**: preferir aggregate como default. Si el diseño específico requiere proyección
o process manager, se decide en la fase de descubrimiento — no como default.

#### 6. Heurísticas, no principios

Decisión lingüística importante: estas son **heurísticas de diseño**, no principios absolutos.
La distinción no es cosmética — significa que el diseño específico de cada caso puede ajustarse
en el event-stormer o planner sin que sea una violación de reglas. Da flexibilidad sin dar
licencia para la inconsistencia.

---

### Artefactos generados

- `docs/adr/0015-estilo-modelado-objetos-dominio.md` — ADR con tabla de heurísticas y ejemplos
- `.claude/agents/implementer.md` — sección nueva "Modelado de objetos de dominio" + reglas 11-13
- `.claude/agents/reviewer.md` — checklist "Modelado de objetos" + items en tabla de reporte + reglas 14-16

---

### Tensiones no resueltas / preguntas abiertas

- ¿Cuándo un aggregate que acumula estado de múltiples dominios cruza la línea y debería
  ser una proyección? No hay criterio cuantitativo — se evalúa caso a caso en el descubrimiento.
- ¿Los eventos con factory static deben ser la norma o la excepción? Por ahora: excepción
  aplicada solo cuando hay precondiciones estructurales claras.
- El `InformacionDia` del ejemplo es un aggregate preliminar sin nombre definitivo — habrá
  que modelarlo formalmente cuando llegue esa historia.
