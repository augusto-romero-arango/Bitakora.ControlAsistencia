---
theme: seriph
title: 'Codificación agéntica: ¿el "Silver Bullet"?'
info: |
  Impacto de la IA en la codificación.
  Reconstrucción a partir del transcript completo.
class: text-center
highlighter: shiki
lineNumbers: false
transition: fade
mdc: true
fonts:
  sans: 'Inter'
  serif: 'EB Garamond'
  mono: 'JetBrains Mono'
---

# Codificación agéntica

### ¿el *"Silver Bullet"*?

<div class="mt-12 text-base opacity-80 max-w-3xl mx-auto leading-relaxed">
En 1986, Fred Brooks sentenció que el software no tendría bala de plata: ninguna tecnología entregaría un orden de magnitud de mejora en productividad, confiabilidad ni simplicidad. Cuarenta años después, parece que ahora sí la tenemos.
</div>

<!--
Referencia: Fred Brooks, "No Silver Bullet — Essence and Accident in Software Engineering", 1986 (ensayo integrado después a la 2ª ed. de The Mythical Man-Month, 1995).
El interrogante es intencional: la charla es la demostración. No se afirma que la tenemos — se explora si lo que tenemos cumple el criterio de Brooks.
Tono interno corporativo — no es TEDx ni conferencia pública.
Durante 40 años nadie refutó a Brooks sin ser ridiculizado. La hipótesis: los agentes de IA sí lo hacen.
-->

---
layout: center
---

## [PENDIENTE: tesis en una línea]

<div class="mt-8 opacity-60 text-sm max-w-3xl mx-auto">
Tesis articuladora de la charla. Se define después del análisis de Brooks (las cuatro dificultades esenciales: complejidad, conformidad, cambiabilidad, invisibilidad) y dónde los agentes las rompen — principalmente cambiabilidad, y amplifican los "ataques prometedores" que el propio Brooks respaldó (buy vs build, refinamiento iterativo, desarrollo incremental, grandes diseñadores).
</div>

<!--
Direcciones candidatas para la tesis:
- La IA amplifica, no resuelve. El oficio, la arquitectura y la organización deciden el signo.
- La codificación agéntica es capacidad nueva; convertirla en resultados exige oficio, arquitectura y organización deliberados.
- Si la codificación agéntica es el Silver Bullet, lo es con oficio, con arquitectura y con una organización preparada — nunca sola.

PENDIENTE: elegir con Augusto en ensayo.
-->

---
layout: quote
---

<img src="/meijer-myconf.jpg" class="mx-auto mb-8 h-40 rounded-sm shadow-sm object-cover" alt="Erik Meijer" />

> "Los días de escribir código a mano están llegando a su fin."

<div class="mt-6 text-right opacity-70">
— Dr. Erik Meijer<br>
<span class="text-sm">Diseñador de Visual Basic, C#, Haskell, LINQ y Hack · 2024</span>
</div>

<div class="mt-8 text-xs opacity-50">
youtube.com/watch?v=SsJqmV3Wtkg · citado en Kim &amp; Yegge, <em>Vibe Coding</em>
</div>

<!--
Original: "The days of writing code by hand are coming to an end."
Meijer dedicó su vida a hacer más fácil escribir código. Que él diga esto no es marketing.
Frase hermana: "We are going to be the last generation of developers to write code by hand, so let's have fun doing it."
Kim & Yegge abren Vibe Coding con esta cita — anchor narrativo del libro.
Ritmo: leer, pausa larga, pasar sin comentar.-->

---
layout: quote
---

<img src="/karpathy.jpg" class="mx-auto mb-8 h-40 rounded-sm shadow-sm object-cover" alt="Andrej Karpathy" />

> "Hay una nueva forma de codificar a la que llamo *vibe coding*: te entregas por completo a las vibras, abrazas lo exponencial y olvidas que el código siquiera existe."

<div class="mt-6 text-right opacity-70">
— Andrej Karpathy<br>
<span class="text-sm">Cofundador de OpenAI · 2 de febrero de 2025</span>
</div>

<div class="mt-8 text-xs opacity-50">
x.com/karpathy/status/1886192184808149383
</div>

<!--
Original: "There's a new kind of coding I call 'vibe coding', where you fully give in to the vibes, embrace exponentials, and forget that the code even exists."
Tweet "throwaway" un sábado por la noche; 4.5M views; dio nombre al movimiento.
Karpathy fundó OpenAI y lideró Autopilot en Tesla.
Kim & Yegge tomaron el término y lo convirtieron en disciplina.
Ritmo: leer despacio, pausa larga, pasar sin comentar.-->

---
layout: quote
---

<img src="/amodei-cfr.jpg" class="mx-auto mb-8 h-40 rounded-sm shadow-sm object-cover" alt="Dario Amodei" />

> "Estamos a tres o seis meses de un mundo en el que la IA escribe el 90 % del código. Y en doce meses, podríamos estar en un mundo en el que la IA escribe prácticamente todo el código."

<div class="mt-6 text-right opacity-70">
— Dario Amodei<br>
<span class="text-sm">CEO de Anthropic · Council on Foreign Relations · 10 de marzo de 2025</span>
</div>

<!--
Original: "What we are finding is that we're 3 to 6 months from a world where AI is writing 90 percent of the code. And then in 12 months, we may be in a world where AI is writing essentially all of the code."
Amodei fue VP of Research en OpenAI (lideró GPT-2 y GPT-3) antes de fundar Anthropic.
Habla desde quien construye los modelos.
Hoy (abril 2026) el timeline no se cumplió en el pie de la letra — Daring Fireball publicó un "claim chowder" en marzo 2026. Fuera del slide. Se maneja en Q&A.
Seis meses después del primer anuncio Amodei confirmó: "Within Anthropic and within a number of companies that we work with, that is absolutely true now."-->

---
layout: quote
---

<img src="/lennys-cherny.jpg" class="mx-auto mb-8 h-40 rounded-sm shadow-sm object-cover" alt="Boris Cherny" />

> "La codificación está en gran parte resuelta."

<div class="mt-6 text-right opacity-70">
— Boris Cherny<br>
<span class="text-sm">Head of Claude Code en Anthropic · Lenny's Podcast · febrero de 2026</span>
</div>

<div class="mt-8 text-xs opacity-50">
youtu.be/We7BZVKbCVw
</div>

<!--
Original: "At this point, it is safe to say that coding is largely solved. At least for the kind of programming that I do, it's just a solved problem because Claude can do it."
Cherny lidera Claude Code. Es quien construye la herramienta.
Matices de la misma entrevista (para la pasada):
- "Claude is starting to come up with ideas... a little more like a co-worker."
- "By the end of the year the title software engineer is going to start to go away and it's just going to be replaced by builder."
- "Everyone codes. Our product manager codes, our engineering manager codes, our designer codes."
Anclaje narrativo: Meijer declara el fin (futuro), Karpathy lo nombra, Amodei predice el timeline, Cherny dice "ya está resuelto, desde adentro".-->

---
layout: quote
---

<img src="/kent-beck.jpg" class="mx-auto mb-8 h-40 rounded-sm shadow-sm object-cover" alt="Kent Beck" />

> "Los juniors que trabajan así comprimen su curva dramáticamente. Tareas que antes tomaban días, ahora toman horas."

<div class="mt-6 text-right opacity-70">
— Kent Beck<br>
<span class="text-sm">Creador de XP y TDD · <em>Augmented Coding: Beyond the Vibes</em> · 2025</span>
</div>

<div class="mt-8 text-xs opacity-50">
tidyfirst.substack.com
</div>

<!--
Original: "The juniors working this way compress their ramp dramatically. Tasks that used to take days take hours."
Citado por Simon Willison: https://simonwillison.net/2025/Dec/16/kent-beck/
Kent Beck es creador de XP, TDD, cofirmante del Agile Manifesto. Su validación pesa.
Distinción clave que hace Beck: "augmented coding" (usar IA para acelerar manteniendo calidad) ≠ "vibe coding" en el sentido pasivo. Los autores del libro también hacen esa distinción.-->

---
layout: quote
---

<img src="/yegge-revenge.png" class="mx-auto mb-8 h-40 rounded-sm shadow-sm object-cover" alt="Steve Yegge" />

> "No caigas en la tentadora trampa de aplazar el trabajo. Decir 'en seis meses será mucho más rápido, así que voy a posponer esto seis meses' es como decir 'voy a esperar a que el tráfico baje'. Tu trayecto será más corto, claro. Pero llegarás de último."

<div class="mt-6 text-right opacity-70">
— Steve Yegge<br>
<span class="text-sm">Coautor de <em>Vibe Coding</em> · "Revenge of the Junior Developer" · marzo de 2025</span>
</div>

<div class="mt-8 text-xs opacity-50">
sourcegraph.com/blog/revenge-of-the-junior-developer · citado en Kim &amp; Yegge, <em>Vibe Coding</em>
</div>

<!--
Original: "Don't fall prey to the tempting work-deferral trap. Saying 'It'll be way faster in 6 months, so I'll just push this work out 6 months' is like saying, 'I'm going to wait until traffic dies down.' Your drive will be shorter, sure. But you will arrive last."
Kim & Yegge usan este pasaje en el arranque del libro como anclaje del work-deferral trap.
Por qué pega: el líder senior con poca mano en el código es el target exacto del argumento. "Mejor espero a que madure" es la trampa concreta que queremos desmontar.
Quién es Yegge: décadas en Amazon, Google, Grab, Sourcegraph. Blogger legendario.
Ritmo: leer completo. Pausa larga tras "llegarás de último".-->

---
layout: quote
---

<img src="/gas-town.webp" class="mx-auto mb-8 h-40 rounded-sm shadow-sm object-cover" alt="Gas Town (Steve Yegge)" />

> "El foco es *throughput*: creación y corrección a la velocidad del pensamiento. En Gas Town dejas que Claude Code haga lo suyo. Tú eres el Product Manager y Gas Town es un compilador de ideas."

<div class="mt-6 text-right opacity-70">
— Steve Yegge<br>
<span class="text-sm"><em>Welcome to Gas Town</em> · enero de 2026</span>
</div>

<div class="mt-8 text-xs opacity-50">
steve-yegge.medium.com/welcome-to-gas-town-4f25ee16dd04
</div>

<!--
Original: "The focus is throughput: creation and correction at the speed of thought." / "In Gas Town, you let Claude Code do its thing. You are a Product Manager, and Gas Town is an Idea Compiler."
Gas Town es un orquestador de agentes que Yegge construyó y liberó en enero 2026. En 12 días: 100+ PRs, ~50 contribuidores, 189k líneas de Go.
El punto: ya no es "usar IA para escribir código" — es "orquestar decenas de agentes". Yegge dice que tiene 20-30 Claudes trabajando en paralelo. El oficio cambió.
Mencionar al pasar "The AI Vampire" (feb 2026) que cuenta el lado oscuro: fatiga, burnout. Sweet spot del día de trabajo nuevo: 3-4 horas.-->

---
layout: statement
---

## Pero no todo es promesa.

<!--
Slide de transición entre voces de la promesa y voces críticas.
Pausa. Cambio de tono.
El auditorio necesita saber que no vamos a ignorar el miedo legítimo.
-->

---
layout: quote
class: text-red-600
---

<img src="/young.jpg" class="mx-auto mb-8 h-40 rounded-sm shadow-sm object-cover" alt="Jessie Young" />

> "Díganme que al menos vamos a probar estos sistemas generados por IA antes de empezar a cobrarle a la gente."

<div class="mt-6 text-right opacity-70">
— Jessie Young<br>
<span class="text-sm">Principal Engineer, GitLab · <em>No Vibe Coding While I'm On Call</em> · IT Revolution, 2025</span>
</div>

<div class="mt-8 text-xs opacity-50">
itrevolution.com/articles/no-vibe-coding-while-im-on-call-what-happens-when-ai-writes-your-production-code
</div>

<!--
Original: "Tell me we're at least going to test these AI-generated systems before we start taking people's money."
Publicado en el Fall 2025 Enterprise Technology Leadership Journal. Jessie Young es Principal Engineer en GitLab (área Manage y AI-Powered Stages).
El artículo es una ficción que narra incidentes de producción en una empresa ficticia por adopción sin control de vibe coding.
Esta es la contravoz legítima. No es tecnofobia; es alguien que está de guardia a las 2 a.m.
El título del artículo — "no vibe coding mientras estoy de turno" — es la bandera del miedo razonable.
Frase hermana: "This is how we use AI responsibly."-->

---
layout: default
class: text-center
---

# DORA 2024:<br>la anomalía GenAI

<div class="mt-8 text-sm opacity-70 max-w-2xl mx-auto">
DORA (DevOps Research and Assessment) es el programa de investigación fundado por Nicole Forsgren, Jez Humble y Gene Kim, hoy parte de Google Cloud. Publica anualmente el <em>Accelerate State of DevOps Report</em> con base en decenas de miles de profesionales.
</div>

<div class="mt-8 grid grid-cols-2 gap-12 text-5xl font-serif">
  <div>
    <div class="text-red-600">−1.5%</div>
    <div class="text-base opacity-70 mt-2">throughput<br>de entrega</div>
  </div>
  <div>
    <div class="text-red-600">−7.2%</div>
    <div class="text-base opacity-70 mt-2">estabilidad<br>de entrega</div>
  </div>
</div>

<div class="mt-8 text-sm opacity-60">
Accelerate State of DevOps 2024 · dora.dev
</div>

<!--
Fuente: https://dora.dev/research/2024/dora-report/ y https://dora.dev/ai/gen-ai-report/
El 76 % de los desarrolladores usan IA para alguna tarea diaria. La adopción subió la calidad de la documentación 7.5 %, la calidad del código 3.4 %, y la velocidad de revisión 3.1 %.
PERO: el throughput de entrega bajó 1.5 % y la estabilidad 7.2 %.
Explicación candidata (Vacuum Hypothesis): el tiempo liberado por la IA se absorbe en otras tareas de bajo valor, en vez de traducirse en más delivery valioso.
Nathen Harvey (DORA lead, Google Cloud): probablemente el código escrito por IA necesita arreglos antes de producción que consumen el tiempo ganado.
Explicación breve en la pasada: DORA es el organismo que define qué es "alto desempeño" en entrega de software. Sus métricas son la referencia de la industria desde 2014.
-->

---
layout: default
class: text-center
---

# Estudio METR, julio de 2025

<div class="mt-6 text-sm opacity-70 max-w-2xl mx-auto">
METR (Model Evaluation & Threat Research) es una organización sin ánimo de lucro dedicada a la evaluación empírica de sistemas de IA. El estudio es un ensayo controlado aleatorizado sobre 16 desarrolladores experimentados trabajando en 246 tareas reales.
</div>

<div class="mt-10 grid grid-cols-3 gap-8 font-serif">
  <div>
    <div class="text-xl opacity-70">esperaban</div>
    <div class="text-5xl mt-3">+24%</div>
    <div class="opacity-60 mt-2 text-sm">más rápidos</div>
  </div>
  <div>
    <div class="text-xl opacity-70">creyeron que fueron</div>
    <div class="text-5xl mt-3">+20%</div>
    <div class="opacity-60 mt-2 text-sm">más rápidos</div>
  </div>
  <div>
    <div class="text-xl text-red-600">en realidad fueron</div>
    <div class="text-5xl mt-3 text-red-600">−19%</div>
    <div class="opacity-60 mt-2 text-sm">más lentos</div>
  </div>
</div>

<div class="mt-6 text-xs opacity-60">
metr.org · arxiv.org/abs/2507.09089
</div>

<!--
Fuente: https://metr.org/blog/2025-07-10-early-2025-ai-experienced-os-dev-study/
Paper: https://arxiv.org/abs/2507.09089
16 devs con 5+ años de experiencia en repos maduros (22k+ estrellas, 1M+ líneas de código). 246 tareas asignadas aleatoriamente a "con IA" o "sin IA".
Resultado: con IA, 19 % MÁS LENTOS. Usaban principalmente Cursor Pro con Claude 3.5/3.7 Sonnet (state of the art en ese momento).
El dato más perturbador: el "perception gap". Ni antes ni después del estudio los devs reconocieron la lentitud. "Siento que soy más productivo con IA" no es evidencia de nada.
Implicación: si no se mide, no se sabe. La intuición no basta.
Contexto breve en la pasada: METR es la organización que corrió el estudio más riguroso hasta la fecha sobre productividad con IA. RCT, no encuesta, no autoreporte.
-->

---
layout: statement
---

## La IA amplifica<br>lo bueno y lo malo<br>de las empresas.

<!--
Esta frase es la bisagra del relato. Viene del contexto del cap. 17 del libro y es la tesis que va a medir el DORA 2025.
Si la empresa tiene prácticas rigurosas, la IA las acelera. Si tiene prácticas frágiles, la IA las vuelve catastróficas a velocidad.
Eso es lo que el DORA GenAI Anomaly 2024 ya mostró: amplificó la fragilidad existente.
Pausa larga. Esta frase abre la siguiente sección.
-->

---
layout: default
---

## El tuit de Karpathy fue un tuit.<br>Kim y Yegge lo convirtieron en disciplina.

<div class="mt-12 opacity-80 text-lg">

*Vibe Coding: Building Production-Grade Software With GenAI, Chat, Agents, and Beyond* — Gene Kim &amp; Steve Yegge (2025).

</div>

<div class="mt-6 opacity-60 text-sm">
Dario Amodei escribió el prólogo. El libro lo firman Kim y Yegge.
</div>

<!--
El libro desacopla el término del sentido casual original. Ya no es "aceptar lo que sale"; es un oficio con frameworks, loops, metáforas y prácticas.
Gene Kim: autor de The Phoenix Project y The DevOps Handbook. Steve Yegge: 30 años en la industria, ex-Google y ex-Amazon.
Dario Amodei (cofundador y CEO de Anthropic) escribió el prólogo — no es coautor.

PENDIENTE visual: portada del libro Vibe Coding al lado del texto. Descargar de editorial IT Revolution.
-->

---
layout: default
---

# FAAFO

<div class="mt-8 grid grid-cols-5 gap-6 font-serif">
  <div>
    <div class="text-6xl">F</div>
    <div class="mt-2">Fast</div>
    <div class="opacity-60 text-sm">Rápido</div>
  </div>
  <div>
    <div class="text-6xl">A</div>
    <div class="mt-2">Ambitious</div>
    <div class="opacity-60 text-sm">Ambicioso</div>
  </div>
  <div>
    <div class="text-6xl">A</div>
    <div class="mt-2">Autonomous</div>
    <div class="opacity-60 text-sm">Autónomo</div>
  </div>
  <div>
    <div class="text-6xl">F</div>
    <div class="mt-2">Fun</div>
    <div class="opacity-60 text-sm">Divertido</div>
  </div>
  <div>
    <div class="text-6xl">O</div>
    <div class="mt-2">Optionality</div>
    <div class="opacity-60 text-sm">Opcionalidad</div>
  </div>
</div>

<div class="mt-10 opacity-70 text-sm">
El acrónimo con el que Kim y Yegge describen el beneficio del vibe coding bien hecho.
</div>

<!--
FAAFO desplaza a "debugging" como loop mental dominante. Antes se preguntaba "¿cómo encuentro el bug?"; ahora se pregunta "¿qué tan rápido puedo iterar entre FAAFO?".
Ambitious: atacar problemas que antes no se intentaban.
Autonomous: delegar al agente con supervisión ligera.
Optionality: probar 3 caminos donde antes solo se podía probar 1.
Fuente: libro Vibe Coding + review de Mike Hadlow.
-->

---
layout: default
---

# Inner loop

<div class="mt-8 text-3xl font-serif opacity-90">
Segundos a minutos.
</div>

<div class="mt-8 max-w-3xl opacity-80">
La conversación momento a momento con el agente: escribir un prompt, recibir una propuesta, aceptar o corregir.

Es donde la mayoría de la gente ya aprendió a trabajar con IA. Es también donde terminan la mayoría de los pilotos sin madurar.
</div>

<div class="mt-12 opacity-60 text-sm">
Kim &amp; Yegge · <em>Vibe Coding</em>, Parte 3.
</div>

<!--
El inner loop es el que ocurre en el editor. La métrica natural aquí es "¿cuánto tardo en aceptar o rechazar una sugerencia?".
Casi todos están aquí. Pero quedarse solo aquí es subexplotar el agente.
-->

---
layout: default
---

# Middle loop

<div class="mt-8 text-3xl font-serif opacity-90">
Horas a días.
</div>

<div class="mt-8 max-w-3xl opacity-80">
Sesiones del agente que entran al armario y olvidan todo al salir. Gestión de contexto, memoria de la sesión, planeación táctica.

Es donde aparecen los primeros problemas serios: contextos que se pierden, decisiones que el agente no recuerda, trabajo que se repite.
</div>

<div class="mt-12 opacity-60 text-sm">
Kim &amp; Yegge · <em>Vibe Coding</em>, Parte 3.
</div>

<!--
El middle loop es donde se construye la disciplina de orquestación. ADRs, planes persistidos, notas para el agente, repos de prompts.
Harness engineering vive aquí.
-->

---
layout: default
---

# Outer loop

<div class="mt-8 text-3xl font-serif opacity-90">
Semanas a meses.
</div>

<div class="mt-8 max-w-3xl opacity-80">
Arquitectura, workflows, sostenibilidad del sistema. Prácticas que atraviesan varios agentes y varios desarrolladores.

Es donde se decide si el agente acelera al equipo o si amplifica su fragilidad.
</div>

<div class="mt-12 opacity-60 text-sm">
Kim &amp; Yegge · <em>Vibe Coding</em>, Parte 3.
</div>

<!--
El outer loop es el que diferencia a los equipos que crecen con IA de los que se ahogan.
Aquí vive todo lo que sigue: arquitectura que favorece agentes, capa 3 de la organización, DORA como espejo.
Fuente: https://itrevolution.com/articles/the-three-developer-loops-a-new-framework-for-ai-assisted-coding/
-->

---
layout: default
class: text-center
---

## El Vibe Coding Developer Loop

<img src="/vibe-coding-developer-loop.png" class="mx-auto mt-4 max-w-4xl rounded-sm shadow-sm" />

<div class="mt-4 opacity-70 text-sm">
IT Revolution · Kim &amp; Yegge · 2025
</div>

<!--
El diagrama del loop interno:
1. Frame your objective — establecer el objetivo con el agente
2. Decompose the tasks — descomponer en pasos manejables
3. Start the conversation — pedir el plan o código inicial
4. Review with care — revisar lo que el agente produce
5. Test and verify — validar con pruebas
6. Refine and iterate — iterar hasta lograr el objetivo
7. Automate your own workflow — eliminar fricción

Frase clave del artículo: "You can't fall asleep at the wheel." No se puede dormir uno al volante.
Fuente: https://itrevolution.com/articles/the-vibe-coding-loop/

Nota: el PNG actual es una captura del libro. Si se puede recrear con fidelidad (círculos numerados, mismas etapas) sería mejor para la estética del deck.
-->

---
layout: default
---

## Head chef mindset

<div class="mt-10 font-serif text-xl space-y-2">

El *line cook* sigue la receta.<br>
El *sous chef* ejecuta con responsabilidad.<br>
El *head chef* dirige la cocina.

</div>

<div class="mt-12 opacity-80 max-w-3xl">
La IA convierte a cada desarrollador en head chef potencial. Pero solo si se aprende a serlo.
</div>

<div class="mt-8 opacity-60 text-sm">
<em>Vibe Coding</em>, capítulo 12.
</div>

<!--
Un head chef no hace la sopa él mismo; decide qué sopa hay que hacer, con qué ingredientes, cuándo sale, y coordina al equipo.
Hasta hace un año, casi todos éramos line cooks o sous chefs. Ahora, cada desarrollador puede dirigir un equipo de agentes.
Eso requiere habilidades que antes no se ejercían: diseño, criterio, delegación, verificación, gusto.
El libro habla de la transición de "sous chef responsible for your individual work" a "head chef managing a team of chef robots".
-->

---
layout: default
---

## Caso Adidas · capítulo 17

<div class="mt-10 text-center">
  <div class="font-serif text-8xl">+50%</div>
  <div class="mt-4 text-xl opacity-80">de Happy Time</div>
  <div class="mt-2 text-sm opacity-60">tiempo dedicado a trabajo creativo</div>
</div>

<div class="mt-10 text-center opacity-70 max-w-2xl mx-auto">
Piloto con 700 desarrolladores.<br>
Pero con un asterisco crítico.
</div>

<!--
Fuente: libro Vibe Coding, capítulo 17 ("Theory of Constraints in Action"), caso Adidas.
Happy Time: tiempo dedicado a trabajo creativo que disfrutan.
Annoying Time: tiempo peleando con el entorno (legacy, tests rotos, build frágil, deploys complejos).
El hallazgo decisivo viene en la siguiente diapo.
-->

---
layout: statement
---

## El Happy Time<br>fue significativamente mayor<br>en arquitecturas desacopladas.

<!--
Este es el pivote hacia la sección de arquitectura.
Equipos con ERPs legacy integrados y mala automatización de pruebas encontraron a la IA frustrante — "chef con horno roto", dice el libro.
Equipos con arquitecturas desacopladas, buenas pruebas y pipelines limpios vieron el Happy Time dispararse.
Conclusión: la arquitectura no es decoración. Es el acelerador — o el freno — del vibe coding.
-->

---
layout: default
---

## Nuevas habilidades · capítulo 7

<div class="mt-10 font-serif text-lg space-y-4 opacity-90">

Identificar <em>mavens</em> — quienes experimentan primero.<br>
Comunicación como habilidad central.<br>
Criterio para revisar lo que produce el agente.<br>
Gusto — decidir qué vale la pena y qué no.<br>
Oficio para diseñar sin escribir.<br>
Juicio para saber cuándo parar.

</div>

<div class="mt-10 opacity-60 text-sm">
<em>Vibe Coding</em>, capítulo 7 + Parte 4.
</div>

<!--
La experiencia en resolución de problemas no se devaluó. Al contrario, ahora es la habilidad dominante.
El cap. 7 propone que los líderes identifiquen "mavens" (early adopters) para pilotos, y que las skills de IA entren a las decisiones de hiring junto a las de comunicación.
La barrera que desapareció: el lenguaje de programación. Ya no es necesario "saber Python" o "saber Rust" para construir en esos lenguajes. Lo que queda es el oficio.
Buena noticia para los líderes de equipo presentes: su experiencia no se devaluó. Se volvió la palanca dominante.
-->

---
layout: quote
---

## [PENDIENTE: cita articuladora<br>del capítulo de conclusión<br>de *Vibe Coding*]

<div class="mt-6 text-right opacity-70">
— Gene Kim &amp; Steve Yegge
</div>

<!--
PENDIENTE — Augusto: pásame el fragmento del capítulo de conclusión / call to action que señalas como IMPORTANTE. Lo traduzco y lo encuadro aquí.
Referencia: la foto que pasaste en la conversación de ayer es la página 21 del libro donde está la cita articuladora.
Esta slide cierra "Vibe coding como oficio" y abre "Arquitectura que favorece al agente".
-->

---
layout: center
---

# ¿Qué hacemos<br>para vivir del lado<br>del Happy Time?

<div class="mt-12 font-serif text-2xl space-y-2 opacity-90">

Event-Driven Architecture<br>
Event Sourcing<br>
Serverless<br>
Infrastructure as Code

</div>

<!--
Enlaza con Adidas: el Happy Time fue mayor en arquitecturas desacopladas. Cada componente resuelve una fricción específica.
La sección cierra con DORA como espejo (instrumento de verificación).
Mensaje: no es agenda arquitectónica impuesta — son atributos que ya se eligieron en ControlAsistencia y que se ven en la práctica.
-->

---
layout: default
---

# Event-Driven Architecture

<div class="mt-4 text-2xl font-serif">**El chasis del desacople.**</div>

<div class="mt-10 font-serif text-lg space-y-3 opacity-90">

Cambio sin coordinar con cinco equipos.<br>
Módulos hablan por mensajes.<br>
Una parte falla sin tumbar el sistema.<br>
Sumar consumidor no toca al productor.

</div>

<!--
Fricción real hoy: coordinar entre módulos acoplados. EDA permite volumen de cambios asistidos por IA sin que el sistema se parta.
Si el agente produce 10x o 100x más cambios, el costo de coordinar cada cambio tiene que ser ~cero. EDA lo logra.
Contrasta con monolitos con stored procs compartidos y tablas compartidas entre equipos: un agente ahí no puede moverse solo.
-->

---
layout: default
---

# Event Sourcing

<div class="mt-4 text-2xl font-serif">**La historia es la verdad.**</div>

<div class="mt-10 font-serif text-lg space-y-3 opacity-90">

Se guarda lo que pasó.<br>
El esquema evoluciona sin tumbar nada.<br>
El agente reconstruye el porqué.<br>
La auditoría es el sustrato.

</div>

<!--
La verdad vive en los eventos (append-only). El estado se proyecta desde ahí.
Persistencia sin migraciones traumáticas; historia completa para IA y para trazabilidad legal.
Marten solo se menciona en la pasada, no aparece en el slide.
Cambios de diseño = nuevas proyecciones, no ALTER TABLE con downtime.
El historial completo es contexto para el agente. Cuando un incidente pasa, el agente puede reconstruir qué pasó y cuándo.
-->

---
layout: default
---

# Serverless

<div class="mt-4 text-2xl font-serif">**El código es un pasivo.**</div>

<div class="mt-8 font-serif text-base space-y-2 opacity-90">

Cada línea que se escribe es una línea que se mantiene, se defiende y se paga.<br>
Serverless traslada al cloud todo lo que no se quisiera operar: servidores, parches, capacidad, escalado.<br>
Se paga por uso real — no por capacidad reservada.<br>
Menos código propio = menos superficie de bugs, menos deuda, menos riesgo.

</div>

<div class="mt-8 opacity-70 text-sm italic">
"Un enfoque serverless-first asegura carga operativa baja y ciclos de retroalimentación rápidos."
</div>

<div class="mt-2 opacity-60 text-xs">
Anderson, McCann, O'Reilly · <em>The Value Flywheel Effect</em> · IT Revolution, 2022
</div>

<!--
Conecta con la IA: los agentes escriben código a volumen; si el código es pasivo, hay que ser deliberados.
Sirve de puente al slide de IaC.
Reframe tomado de "The Value Flywheel Effect" (cap. de conclusión — "code is a liability").

PENDIENTE visual: portada del libro al lado de la cita.
-->

---
layout: default
---

# Infrastructure as Code

<div class="mt-4 text-2xl font-serif">**Clickops no escala, no se automatiza.**</div>

<div class="mt-10 font-serif text-lg space-y-3 opacity-90">

La infraestructura se declara en código, se revisa en PR, se despliega por pipeline.<br>
Clickops son pasos manuales que ningún pipeline puede reproducir.<br>
El agente de IA lee y modifica archivos de configuración declarativa; el portal gráfico lo deja ciego.

</div>

<!--
Clickops rompe la cadena automatización → repetibilidad; la IA no navega portales gráficos.
En la pasada se amplía: HCL = HashiCorp Configuration Language, el lenguaje declarativo de Terraform para describir recursos en la nube.
Junto con serverless habilitan que un agente levante un dominio completo (infraestructura + código) desde un prompt hasta producción.
-->

---
layout: default
---

# DORA como espejo

<div class="mt-4 text-2xl font-serif">**Si es Happy Time, se ve en las métricas.**</div>

<div class="mt-10 grid grid-cols-2 gap-8 font-serif">
  <div>
    <div class="text-2xl">Lead time</div>
    <div class="opacity-60 text-sm mt-1">qué tan rápido un cambio llega a producción</div>
  </div>
  <div>
    <div class="text-2xl">Deploy frequency</div>
    <div class="opacity-60 text-sm mt-1">cuántas veces por día se despliega</div>
  </div>
  <div>
    <div class="text-2xl">MTTR</div>
    <div class="opacity-60 text-sm mt-1">cuánto se tarda en recuperarse cuando algo falla</div>
  </div>
  <div>
    <div class="text-2xl">Change failure rate</div>
    <div class="opacity-60 text-sm mt-1">qué porcentaje de cambios rompe producción</div>
  </div>
</div>

<!--
DORA convierte "estamos vibe coding" en afirmación verificable. Es el instrumento que confirma si los cuatro componentes previos están aterrizando o no.
Si el lead time es de semanas, la arquitectura no está habilitando vibe coding en serio.
Si el deploy frequency es una vez por sprint, no hay volumen de cambios.
Si el MTTR es de horas, el agente no puede experimentar sin miedo.
Si el change failure rate es alto, la IA amplifica lo roto.
La GenAI Anomaly 2024 se menciona solo en la pasada (conexión con la primera sección).
-->

---
layout: center
---

# El nuevo junior

<div class="mt-10 font-serif text-2xl opacity-90 max-w-3xl mx-auto">
La IA es el desarrollador junior que cada uno acabamos de contratar.
</div>

<!--
Metáfora que conecta todo el arco. La IA es una capacidad que cada líder dirige. La amplificación depende de cómo se ejerce.
Sin remate. El silencio trabaja.
Primera persona del plural inclusiva — cada uno de los presentes, el speaker incluido.
-->

---
layout: default
---

# Las tres capas del trabajo

<div class="mt-10 space-y-6 font-serif">

<div>
<strong>Capa 1 · El trabajo mismo</strong> — donde se crea valor.
</div>

<div>
<strong>Capa 2 · Las herramientas</strong> — con qué se hace.
</div>

<div class="text-2xl">
<strong>Capa 3 · La arquitectura de la organización</strong> — cómo todo se conecta.
</div>

</div>

<div class="mt-12 opacity-60 text-sm">
Kim &amp; Spear · <em>Wiring the Winning Organization</em> · IT Revolution, 2023. Retomado por Kim &amp; Yegge en <em>Vibe Coding</em>, cap. 17.
</div>

<!--
Marco original: Kim + Spear, 10 años de investigación, MIT Sloan, Toyota Production System como inspiración.
"Arquitectura de la organización" es la traducción operativa de "organizational wiring".
Decisión de lenguaje: "arquitectura de la organización" (no "cableado"). Siempre "capa 3" en español, nunca "L3" ni "Layer 3".
Aclarar en la pasada que es más que arquitectura de software — incluye procesos, comunicación, normas, diseño de equipos.
NUMMI Toyota/GM Fremont: misma capa 1, misma capa 2, solo cambiaron la capa 3, y la fábrica pasó de peor a mejor de GM.
-->

---
layout: default
---

# Qué incluye la capa 3

<div class="mt-8 font-serif text-base space-y-3 opacity-90">

Arquitectura de sistemas — cómo se conectan los componentes.<br>
Diseño organizacional — cómo están estructurados los equipos.<br>
Protocolos de comunicación — quién le habla a quién, sobre qué, con qué frecuencia.<br>
Flujos y procesos — cómo se mueve el trabajo de inicio a fin.<br>
Estándares e interfaces — las reglas acordadas entre áreas.<br>
Normas de liderazgo y cultura — cómo actúan y reaccionan las personas.

</div>

<div class="mt-10 opacity-60 text-sm">
Ley de Conway: la arquitectura del software imita la estructura de comunicación del equipo.
</div>

<!--
Aterrizar la capa 3 concretamente antes de decir "es nuestra obligación".
Incluye arquitectura de software — Conway la documentó hace 50 años.
La capa 3 es organización en el sentido amplio, no solo "arquitectura de software".
-->

---
layout: default
---

# La tercera capa es nuestra

<div class="mt-4 text-xl font-serif opacity-90">**La IA va a cambiar nuestras decisiones de capa 3.**</div>

<div class="mt-10 font-serif text-lg space-y-3 opacity-90">

Dirigir al propio agente se vuelve la capa 3 individual de cada dev.<br>
Coordinar a varios devs que ya coordinan agentes es un problema abierto.<br>
Cuando codificar deja de ser el cuello de botella, la organización lo es.

</div>

<!--
En la pasada se nombra el término del libro "capa 3 de capa 3" (Layer 3 of Layer 3) como nomenclatura de los autores; no aparece en el slide.
Paráfrasis de "managing your own team of AI agents is the new individual Layer 3".
Transferencia del cuello de botella a producto/diseño/QA.
DevOps como precedente (shift left, you build it you run it).
Lenguaje: siempre capa 3 en español, nunca L3 ni Layer 3.
-->

---
layout: default
---

# Accionables de la capa 3

<div class="mt-4 text-xl font-serif opacity-90">**Organizar agentes es organización, no tooling.**</div>

<div class="mt-10 font-serif text-base space-y-3 opacity-90">

<div><strong>Subagentes especializados</strong> — cada rol con su contexto y competencia (planner, test-writer, implementer, reviewer).</div>
<div><strong>Generadores y verificadores</strong> — quien genera no verifica. TDD como sustrato.</div>
<div><strong>Documentación compartida</strong> — CLAUDE.md, ADRs, bitácora. El agente lee lo que el equipo decidió.</div>
<div><strong>Paralelismo bien diseñado</strong> — issues con dependencias declaradas, worktrees aislados.</div>
<div><strong>Integración de verificación</strong> — CI/CD, revisión de PR, métricas DORA.</div>

</div>

<div class="mt-8 opacity-60 text-sm">
Kim &amp; Yegge · <em>Vibe Coding</em> · cap. 17, <em>Agent Organization Patterns</em>.
</div>

<!--
Los patrones del libro son ocho — se añaden en la pasada: direct agent communication via MCP, task graph discipline, merge strategies.
Los cinco del slide son los que ya tienen práctica viva en ControlAsistencia.
-->

---
layout: default
---

# Debemos abordar proyectos ambiciosos

<div class="mt-4 text-xl font-serif opacity-90">**Los agentes nos permiten emprender trabajo que antes no era viable.**</div>

<div class="mt-10 font-serif text-base space-y-3 opacity-90">

Cobertura de pruebas exhaustiva en módulos que llevan años sin test.<br>
Documentación técnica y funcional viva — que a su vez alimenta al próximo agente.<br>
Pruebas end-to-end automatizadas donde hoy todo se valida a mano.<br>
Desacople gradual de módulos atados a stacks legacy.<br>
Productos nuevos autocontenidos — explorar sin pagar el costo de antes.

</div>

<!--
Cada líder elige su proyecto ambicioso. No se impone orden ni prioridad.
Mensaje: la capacidad instalada cambió — si se sigue proponiendo lo mismo de siempre, se subutiliza a los agentes.
-->

---
layout: center
---

# Nuestro compromiso

<div class="mt-10 font-serif text-base space-y-4 opacity-90 max-w-3xl mx-auto text-left">

<div>Aprender a dirigir agentes — cada uno, en su propio contexto.</div>
<div>Estudiar y aplicar <strong>harness engineering</strong> — el oficio de domar la IA para que produzca trabajo confiable.</div>
<div>Formalizar la capa 3 — documentación viva, ADRs y protocolos que hagan escalable el trabajo con agentes.</div>

</div>

<!--
Tres verbos de acción: aprender, aplicar, formalizar.
Cada compromiso mapea a una sección previa: el junior → aprender; accionables del cap. 17 → aplicar; capa 3 → formalizar.
Harness engineering en el ecosistema Claude Code/Cursor/Aider: diseño deliberado del entorno del agente (herramientas, contexto, guardrails, orquestación) para obtener trabajo confiable.
Esta es la nota final. No hay pregunta abierta después.
-->

---
layout: default
class: text-xs
---

## Fuentes

<div class="mt-4 grid grid-cols-2 gap-8">

<div>

### Libros

- Gene Kim &amp; Steve Yegge. *Vibe Coding: Building Production-Grade Software With GenAI, Chat, Agents, and Beyond*. IT Revolution, 2025. Prólogo: Dario Amodei.
- Fred Brooks. *No Silver Bullet — Essence and Accident in Software Engineering*, 1986. Integrado en la 2ª ed. de *The Mythical Man-Month*, 1995.
- Gene Kim &amp; Steven Spear. *Wiring the Winning Organization*. IT Revolution, 2023.
- David Anderson, Mark McCann, Michael O'Reilly. *The Value Flywheel Effect*. IT Revolution, 2022.

### Blogs y ensayos

- Andrej Karpathy, X/Twitter, 2 feb 2025 — definición original de vibe coding.
- Steve Yegge, *Revenge of the Junior Developer*, Sourcegraph blog, mar 2025.
- Steve Yegge, *Welcome to Gas Town*, Medium, ene 2026.
- Steve Yegge, *The AI Vampire*, Medium, feb 2026.
- Kent Beck, *Augmented Coding: Beyond the Vibes*, tidyfirst.substack.com, 2025.
- IT Revolution, *The Vibe Coding Loop*, 2025.
- IT Revolution, *The Three Developer Loops*, 2025.

</div>

<div>

### Entrevistas y conferencias

- Dario Amodei, Council on Foreign Relations, 10 mar 2025.
- Boris Cherny, *Head of Claude Code: What happens after coding is solved*, Lenny's Podcast, feb 2026. youtu.be/We7BZVKbCVw
- Dr. Erik Meijer — charla YouTube, 2024 (youtube.com/watch?v=SsJqmV3Wtkg).

### Estudios

- DORA / Google Cloud, *Accelerate State of DevOps 2024* — GenAI Anomaly.
- DORA / Google Cloud, *Impact of Generative AI in Software Development*, 2024.
- METR, *Measuring the Impact of Early-2025 AI on Experienced Open-Source Developer Productivity*, jul 2025. arxiv.org/abs/2507.09089.

### Voces críticas

- Jessie Young et al., *No Vibe Coding While I'm On Call*, IT Revolution — Enterprise Technology Leadership Journal Fall 2025.

</div>

</div>

<!--
URLs reales documentadas aquí para Q&A.
-->

---
layout: end
class: text-center
---

# Gracias

<div class="mt-8 opacity-70">
Preguntas.
</div>

<!--
Cierre. Abierto a preguntas.
-->
