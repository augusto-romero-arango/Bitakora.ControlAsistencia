<template>
  <div class="vcl">
    <svg
      viewBox="0 0 1000 760"
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

      <!-- OUTER LOOP arcs: 1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 1 -->
      <g stroke="currentColor" stroke-width="2" fill="none">
        <path d="M 249 415 A 270 270 0 0 1 351 179" marker-end="url(#vcl-arrow)" />
        <path d="M 381 158 A 270 270 0 0 1 619 158" marker-end="url(#vcl-arrow)" />
        <path d="M 649 179 A 270 270 0 0 1 751 415" marker-end="url(#vcl-arrow)" />
        <path d="M 751 425 A 270 270 0 0 1 649 661" marker-end="url(#vcl-arrow)" />
        <path d="M 619 682 A 270 270 0 0 1 381 682" marker-end="url(#vcl-arrow)" />
        <path d="M 351 661 A 270 270 0 0 1 249 425" marker-end="url(#vcl-arrow)" />
      </g>

      <!-- INNER LOOP arcs: A -> B -> C -> D -> E -> A -->
      <g stroke="currentColor" stroke-width="1.5" fill="none" opacity="0.85">
        <path d="M 562 443 A 65 65 0 0 1 492 457" marker-end="url(#vcl-arrow-thin)" />
        <path d="M 478 444 A 65 65 0 0 1 457 386" marker-end="url(#vcl-arrow-thin)" />
        <path d="M 461 371 A 65 65 0 0 1 504 339" marker-end="url(#vcl-arrow-thin)" />
        <path
          d="M 522 338 A 65 65 0 0 1 569 372"
          marker-end="url(#vcl-arrow-thin)"
          stroke-dasharray="4 3"
        />
        <path d="M 572 386 A 65 65 0 0 1 561 427" marker-end="url(#vcl-arrow-thin)" />
      </g>

      <!-- 4 -> A (entrada del loop interno desde el paso 4) -->
      <g stroke="currentColor" stroke-width="1.5" fill="none" opacity="0.85">
        <path d="M 746 410 Q 670 440 582 440" marker-end="url(#vcl-arrow-thin)" />
      </g>

      <!-- OUTER nodes -->
      <g>
        <circle cx="230" cy="400" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="230" y="400" text-anchor="middle" dominant-baseline="middle">1</text>
        <text class="label-out" x="198" y="400" text-anchor="end" dominant-baseline="middle">
          <tspan x="198" dy="-9">Separar</tspan>
          <tspan x="198" dy="18">subtarea</tspan>
        </text>

        <circle cx="365" cy="166" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="365" y="166" text-anchor="middle" dominant-baseline="middle">2</text>
        <text class="label-out" x="365" y="116" text-anchor="middle">
          <tspan x="365" dy="0">Iniciar conversación</tspan>
          <tspan x="365" dy="18">con IA</tspan>
        </text>

        <circle cx="635" cy="166" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="635" y="166" text-anchor="middle" dominant-baseline="middle">3</text>
        <text class="label-out" x="635" y="116" text-anchor="middle">
          <tspan x="635" dy="0">Crear plan</tspan>
          <tspan x="635" dy="18">con IA</tspan>
        </text>

        <circle cx="770" cy="400" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="770" y="400" text-anchor="middle" dominant-baseline="middle">4</text>
        <text class="label-out" x="802" y="400" text-anchor="start" dominant-baseline="middle">
          <tspan x="802" dy="-9">La IA ejecuta</tspan>
          <tspan x="802" dy="18">el plan</tspan>
        </text>

        <circle cx="635" cy="634" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="635" y="634" text-anchor="middle" dominant-baseline="middle">5</text>
        <text class="label-out" x="635" y="684" text-anchor="middle">
          <tspan x="635" dy="0">Probar y</tspan>
          <tspan x="635" dy="18">verificar</tspan>
        </text>

        <circle cx="365" cy="634" r="22" fill="none" stroke="currentColor" stroke-width="2" />
        <text class="num" x="365" y="634" text-anchor="middle" dominant-baseline="middle">6</text>
        <text class="label-out" x="365" y="684" text-anchor="middle">
          <tspan x="365" dy="0">Refinar e</tspan>
          <tspan x="365" dy="18">iterar</tspan>
        </text>
      </g>

      <!-- INNER nodes (A..E), centro aproximado (520, 400), r=65 -->
      <g>
        <circle cx="570" cy="443" r="14" fill="none" stroke="currentColor" stroke-width="1.5" />
        <text class="letter" x="570" y="443" text-anchor="middle" dominant-baseline="middle">A</text>
        <text class="label-in" x="592" y="449" text-anchor="start">
          <tspan x="592" dy="0">Escribir</tspan>
          <tspan x="592" dy="13">código</tspan>
        </text>

        <circle cx="485" cy="458" r="14" fill="none" stroke="currentColor" stroke-width="1.5" />
        <text class="letter" x="485" y="458" text-anchor="middle" dominant-baseline="middle">B</text>
        <text class="label-in" x="485" y="485" text-anchor="middle">Compilar</text>

        <circle cx="456" cy="390" r="14" fill="none" stroke="currentColor" stroke-width="1.5" />
        <text class="letter" x="456" y="390" text-anchor="middle" dominant-baseline="middle">C</text>
        <text class="label-in" x="436" y="390" text-anchor="end" dominant-baseline="middle">Ejecutar</text>

        <circle cx="513" cy="336" r="14" fill="none" stroke="currentColor" stroke-width="1.5" />
        <text class="letter" x="513" y="336" text-anchor="middle" dominant-baseline="middle">D</text>
        <text class="label-in" x="513" y="315" text-anchor="middle">Probar</text>

        <circle cx="578" cy="372" r="14" fill="none" stroke="currentColor" stroke-width="1.5" />
        <text class="letter" x="578" y="372" text-anchor="middle" dominant-baseline="middle">E</text>
        <text class="label-in" x="598" y="366" text-anchor="start" dominant-baseline="middle">Depurar</text>
      </g>
    </svg>
  </div>
</template>

<style scoped>
.vcl {
  width: 100%;
  max-width: 860px;
  margin: 0 auto;
}
.vcl svg {
  width: 100%;
  height: auto;
  display: block;
  overflow: visible;
}
.vcl :deep(text) {
  fill: currentColor;
  font-family: inherit;
  font-size: 14px !important;
  line-height: 1;
}
.vcl :deep(text.num) {
  font-size: 20px !important;
  font-weight: 700;
}
.vcl :deep(text.letter) {
  font-size: 13px !important;
  font-weight: 700;
}
.vcl :deep(text.label-out) {
  font-size: 14px !important;
}
.vcl :deep(text.label-in) {
  font-size: 11px !important;
  opacity: 0.9;
}
</style>
