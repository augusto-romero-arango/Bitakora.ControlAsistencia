# Field Note — 2026-04-05
## Review PR #6 y formalización de heurísticas de diseño para value objects

### Tipo de sesión
Review de código / decisiones de diseño / actualización de agentes

---

### Contexto

El PR #6 (HU-2: modelar value objects de franja temporal) pasó el pipeline TDD completo
con los tres agentes pero el dueño del producto identificó problemas serios en el diseño
producido. La sesión partió del análisis de los comentarios del PR y derivó en una
conversación de diseño que generó heurísticas nuevas incorporadas a los agentes.

Los problemas revelaron dos categorías distintas:
1. **Reglas existentes ignoradas**: .resx para mensajes y verificación de mensaje en
   `ThrowExactly` ya estaban documentadas, pero los agentes no las aplicaron en value objects.
2. **Heurísticas de diseño no documentadas**: encapsulamiento de propiedades internas,
   números mágicos, testear via `ToString()`, validaciones como invariantes del constructor.

---

### Hallazgos del PR #6

#### Encapsulamiento roto en FranjaBase
`HoraInicio`, `HoraFin`, `DiaOffsetFin`, `MinutosAbsolutoInicio`, `MinutosAbsolutoFin` e
`InferirDiaOffset` eran `public`. Estas son propiedades de mecánica interna —el consumidor
no necesita acceder a ellas. La interfaz pública del value object son sus métodos de
comportamiento: `DuracionEnMinutos()`, `DuracionEnHorasDecimales()`, `ToString()`.

Decisión: propiedades de cálculo interno → `protected` o `private`.

#### MinutosAbsoluto abstractos innecesariamente
`MinutosAbsolutoInicio/Fin` eran `abstract` y se repetían idénticos en las tres subclases.
La causa raíz: el constructor base no recibía los offsets, así que cada subclase tenía que
recalcular. Solución acordada: el constructor base recibe ambos offsets con default `0`.
Subclases con offsets fijos (FranjaOrdinaria siempre tiene `offsetInicio=0`) hardcodean
ese valor al llamar al base — la regla vive en la concreta, el cálculo en la base.

#### Números mágicos: 60 y 1440
Los literales `60` y `1440` aparecían directamente en los cálculos. Deben ser constantes
`protected const int MinutosPorHora = 60` y `protected const int MinutosPorDia = 1440`.

#### Dos factories donde uno era siempre superior
`Crear` + `CrearInfiriendoOffset` coexistían. `CrearInfiriendoOffset` tenía una interfaz
más limpia (un parámetro menos, inferencia automática). En este caso concreto, el factory
con inferencia debe ser el único `Crear`. Nota importante: esto es una **evaluación
contextual**, no una regla mecánica —hay casos donde múltiples factories tienen razón de
existir.

#### Contiene/SeSolapan como métodos públicos
Eran métodos públicos de `FranjaOrdinaria`. La intención del dominio era que al construir
una FranjaOrdinaria con descansos o extras, el constructor validara que estuvieran
contenidos y no se solaparan. Solución: `Contiene` y `SeSolapan` → `private`; la validación
ocurre en el factory. Decisión del dueño: mantenerlos como `private` (reutilizables
internamente) pero no expuestos.

#### ToString no debe ser sealed en FranjaBase
Se selló `ToString()` en la base para evitar que los records derivados generaran su propio.
Pero `FranjaOrdinaria` necesita un `ToString` propio que incluya sus hijas. Solución: quitar
`sealed`, dejar que la ordinaria haga override.

#### ToString de FranjaOrdinaria debe mostrar hijas
Formato acordado (condicional):
- Sin hijas: `(08:00-17:00)`
- Con descansos: `(08:00-17:00), Descansos:(10:00-10:30, 13:00-14:00)`
- Con extras: `(08:00-17:00), Extras:(16:00-17:00)`
- Con ambos: `(08:00-17:00), Descansos:(10:00-10:30) Extras:(16:00-17:00)`

Aclaración: el `{nombre}` propuesto inicialmente por el dueño fue descartado —la
FranjaOrdinaria no tiene nombre propio.

#### Labels de ToString también van a .resx
"Descansos" y "Extras" en el `ToString()` son strings que potencialmente llegan al front.
Regla ampliada: **todo string visible** va a .resx, no solo mensajes de excepción.

#### Tests solo verificaban tipo de excepción
Los tests de `ThrowExactly<ArgumentException>()` no incluían `.WithMessage(...)`.
Los mensajes de excepción dan contexto sobre el porqué del error y deben verificarse.

#### Tests verificaban propiedades via getters individuales
`franja.HoraInicio.Should().Be(...)` en lugar de `franja.ToString().Should().Be(...)`.
Los value objects deben testearse por comportamiento. Si los getters son `protected`,
los tests no pueden acceder a ellos —eso es señal de buen diseño.

---

### Decisiones de diseño tomadas en la sesión

#### Constructor base con offsets
```
FranjaBase(TimeOnly horaInicio, TimeOnly horaFin, int diaOffsetInicio = 0, int diaOffsetFin = 0)
```
- `MinutosAbsolutoInicio/Fin` calculados concretamente en la base
- Subclases pasan sus offsets al base (FranjaOrdinaria siempre pasa `offsetInicio: 0`)
- Los valores por defecto son `0`; solo quien deba enviar `1` lo envía

#### Alcance de i18n
La regla de .resx se expande: no es solo para excepciones. Es para **cualquier string
que potencialmente salga al usuario**. Criterio: si el string podría mostrarse en una UI
o en un mensaje de respuesta HTTP, va a .resx.

#### Enforcement: checklist del reviewer
En lugar de una sección de "errores comunes" o pre-flight check separado, las reglas
nuevas se incorporan al checklist de Definition of Done del reviewer. Es el punto de
control natural —si el reviewer las verifica sistemáticamente, no se escapan.

---

### Artefactos generados

- `.claude/agents/test-writer.md`:
  - Sección 6b extendida: aplica a value objects (no solo aggregates/handlers)
  - Nueva sección 6c: testear value objects via `ToString()`, no via getters internos
  - Verificación de mensaje obligatoria en excepciones de value objects
- `.claude/agents/implementer.md`:
  - "Encapsulamiento: propiedades internas" — nueva sección
  - "Números mágicos → constantes con nombre" — nueva sección
  - "Diseño de factories: evaluar si el secundario supera al principal" — nueva sección
  - "Validaciones de consistencia → invariantes del constructor" — nueva sección
  - "i18n: todo string visible en .resx" — nueva sección
- `.claude/agents/reviewer.md`:
  - 6 items nuevos en el checklist de Definition of Done
  - Sección de modelado extendida con heurísticas específicas para value objects
  - Reglas absolutas 17-20 agregadas

Commit: `5ed646c`

---

### Tensiones no resueltas / preguntas abiertas

- **Herencia FranjaDescanso/FranjaExtra**: con el constructor base recibiendo offsets,
  las dos subclases dejan de ser casi idénticas pero siguen siendo estructuralmente
  similares. ¿Merece una clase intermedia `FranjaCruceDia`? Se pospone para cuando
  el diseño del Turno madure —hay que ver si la duplicación residual sobrevive.
- **CA-3 (serialización JSON)** diferida al issue #3: el constructor vacío `private`
  puede no ser suficiente para Marten según la configuración. Pendiente verificar el
  nivel de acceso requerido.
- **Alcance del encapsulamiento en records con jerarquía**: en C#, `protected` en un
  record padre es accesible desde subclases pero no desde tests. ¿Cómo se testean
  las subclases sin exponer las propiedades? La respuesta acordada es via `ToString()` —
  pero hay que verificar que `ToString()` de subclases exponga suficiente información.
