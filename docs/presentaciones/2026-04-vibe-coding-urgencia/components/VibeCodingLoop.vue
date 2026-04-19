<template>
  <div class="vcl">
    <svg
      viewBox="0 0 1000 640"
      xmlns="http://www.w3.org/2000/svg"
      preserveAspectRatio="xMidYMid meet"
    >
      <defs>
        <marker
          id="vcl-arrow"
          viewBox="0 0 10 10"
          refX="9"
          refY="5"
          markerWidth="7"
          markerHeight="7"
          orient="auto-start-reverse"
        >
          <path d="M 0 0 L 10 5 L 0 10 z" fill="currentColor" />
        </marker>
        <marker
          id="vcl-arrow-thin"
          viewBox="0 0 10 10"
          refX="9"
          refY="5"
          markerWidth="6"
          markerHeight="6"
          orient="auto-start-reverse"
        >
          <path d="M 0 0 L 10 5 L 0 10 z" fill="currentColor" opacity="0.8" />
        </marker>
      </defs>

      <!-- OUTER LOOP arcs: centro (500,340) radio 240. 1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 1 -->
      <g stroke="currentColor" stroke-width="2" fill="none">
        <path d="M 261 319 A 240 240 0 0 1 362 144" marker-end="url(#vcl-arrow)" />
        <path d="M 399 123 A 240 240 0 0 1 601 123" marker-end="url(#vcl-arrow)" />
        <path d="M 638 144 A 240 240 0 0 1 739 319" marker-end="url(#vcl-arrow)" />
        <path d="M 739 361 A 240 240 0 0 1 638 536" marker-end="url(#vcl-arrow)" />
        <path d="M 601 557 A 240 240 0 0 1 399 557" marker-end="url(#vcl-arrow)" />
        <path d="M 362 536 A 240 240 0 0 1 261 361" marker-end="url(#vcl-arrow)" />
      </g>

      <!-- INNER LOOP arcs: centro (520, 340), radio 60. A -> B -> C -> D -> E -> A -->
      <g stroke="currentColor" stroke-width="1.5" fill="none" opacity="0.85">
        <path d="M 553 390 A 60 60 0 0 1 504 398" marker-end="url(#vcl-arrow-thin)" />
        <path d="M 483 387 A 60 60 0 0 1 460 343" marker-end="url(#vcl-arrow-thin)" />
        <path d="M 464 319 A 60 60 0 0 1 499 284" marker-end="url(#vcl-arrow-thin)" />
        <path
          d="M 523 280 A 60 60 0 0 1 567 303"
          marker-end="url(#vcl-arrow-thin)"
          stroke-dasharray="4 3"
        />
        <path d="M 578 324 A 60 60 0 0 1 570 373" marker-end="url(#vcl-arrow-thin)" />
      </g>

      <!-- 4 -> A: entrada al loop interno desde el paso 4 -->
      <g stroke="currentColor" stroke-width="1.5" fill="none" opacity="0.85">
        <path d="M 719 345 Q 660 370 574 379" marker-end="url(#vcl-arrow-thin)" />
      </g>

      <!-- OUTER nodes -->
      <g>
        <circle cx="260" cy="340" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="260" y="340" text-anchor="middle" dominant-baseline="middle">1</text>
        <text class="label-out" x="226" y="340" text-anchor="end">
          <tspan x="226" dy="-5">Separar</tspan>
          <tspan x="226" dy="16">subtarea</tspan>
        </text>

        <circle cx="380" cy="132" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="380" y="132" text-anchor="middle" dominant-baseline="middle">2</text>
        <text class="label-out" x="380" y="82" text-anchor="middle">
          <tspan x="380" dy="0">Iniciar conversación</tspan>
          <tspan x="380" dy="16">con IA</tspan>
        </text>

        <circle cx="620" cy="132" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="620" y="132" text-anchor="middle" dominant-baseline="middle">3</text>
        <text class="label-out" x="620" y="82" text-anchor="middle">
          <tspan x="620" dy="0">Crear plan</tspan>
          <tspan x="620" dy="16">con IA</tspan>
        </text>

        <circle cx="740" cy="340" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="740" y="340" text-anchor="middle" dominant-baseline="middle">4</text>
        <text class="label-out" x="774" y="340" text-anchor="start">
          <tspan x="774" dy="-5">La IA ejecuta</tspan>
          <tspan x="774" dy="16">el plan</tspan>
        </text>

        <circle cx="620" cy="548" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="620" y="548" text-anchor="middle" dominant-baseline="middle">5</text>
        <text class="label-out" x="620" y="598" text-anchor="middle">
          <tspan x="620" dy="0">Probar y</tspan>
          <tspan x="620" dy="16">verificar</tspan>
        </text>

        <circle cx="380" cy="548" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="380" y="548" text-anchor="middle" dominant-baseline="middle">6</text>
        <text class="label-out" x="380" y="598" text-anchor="middle">
          <tspan x="380" dy="0">Refinar e</tspan>
          <tspan x="380" dy="16">iterar</tspan>
        </text>
      </g>

      <!-- INNER nodes (A..E) -->
      <g>
        <circle cx="562" cy="382" r="12" fill="none" stroke="currentColor" stroke-width="1.5" />
        <text class="letter" x="562" y="382" text-anchor="middle" dominant-baseline="middle">A</text>
        <text class="label-in" x="580" y="382" text-anchor="start">
          <tspan x="580" dy="-4">Escribir</tspan>
          <tspan x="580" dy="12">código</tspan>
        </text>

        <circle cx="493" cy="394" r="12" fill="none" stroke="currentColor" stroke-width="1.5" />
        <text class="letter" x="493" y="394" text-anchor="middle" dominant-baseline="middle">B</text>
        <text class="label-in" x="493" y="420" text-anchor="middle">Compilar</text>

        <circle cx="461" cy="331" r="12" fill="none" stroke="currentColor" stroke-width="1.5" />
        <text class="letter" x="461" y="331" text-anchor="middle" dominant-baseline="middle">C</text>
        <text class="label-in" x="441" y="331" text-anchor="end" dominant-baseline="middle">Ejecutar</text>

        <circle cx="511" cy="281" r="12" fill="none" stroke="currentColor" stroke-width="1.5" />
        <text class="letter" x="511" y="281" text-anchor="middle" dominant-baseline="middle">D</text>
        <text class="label-in" x="511" y="260" text-anchor="middle">Probar</text>

        <circle cx="574" cy="313" r="12" fill="none" stroke="currentColor" stroke-width="1.5" />
        <text class="letter" x="574" y="313" text-anchor="middle" dominant-baseline="middle">E</text>
        <text class="label-in" x="592" y="313" text-anchor="start" dominant-baseline="middle">Depurar</text>
      </g>
    </svg>
  </div>
</template>

<style scoped>
.vcl {
  width: 100%;
  text-align: center;
}
.vcl svg {
  width: 680px;
  max-width: 100%;
  height: auto;
  display: inline-block;
  overflow: visible;
}
.vcl :deep(text) {
  fill: currentColor;
  font-family: inherit;
  font-size: 13px !important;
  line-height: 1;
}
.vcl :deep(text.num) {
  font-size: 19px !important;
  font-weight: 700;
}
.vcl :deep(text.letter) {
  font-size: 12px !important;
  font-weight: 700;
}
.vcl :deep(text.label-out) {
  font-size: 13px !important;
}
.vcl :deep(text.label-in) {
  font-size: 10px !important;
  opacity: 0.9;
}
</style>
