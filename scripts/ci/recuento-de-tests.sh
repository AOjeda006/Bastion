#!/usr/bin/env bash
#
# Emite cuántos casos de test ha ejecutado de verdad un paso de la CI, y falla si son
# menos de los exigidos.
#
# POR QUÉ EXISTE
# --------------
# `dotnet test` sale con código 0 en tres situaciones que en la página del run se ven
# EXACTAMENTE igual de verdes:
#
#   1. ha ejecutado los casos y han pasado;
#   2. el `--filter` no ha casado con ninguno ("Ninguna prueba coincide con el filtro");
#   3. no ha encontrado ningún ensamblado de test (le pasó al ítem 0.1).
#
# Los registros de un job devuelven 403 sin autenticar, así que desde fuera no hay forma
# de distinguirlas. Las ANOTACIONES sí son públicas: de ahí el `::notice::`.
#
# POR QUÉ LEE EL .trx Y NO LA SALIDA DE CONSOLA
# ---------------------------------------------
# El resumen de consola está TRADUCIDO al idioma del CLI ("Correctas! - Con error: 0,
# Superado: 41..." en una máquina en español, "Passed! - Failed: 0, Passed: 41..." en el
# runner). Un `grep` sobre eso funciona en la CI y falla en local, que es la peor de las
# combinaciones. El `.trx` es XML y sus atributos no se traducen.
#
# Uso: recuento-de-tests.sh <directorio-con-trx> <etiqueta> <mínimo-exigido>

set -euo pipefail

DIRECTORIO="${1:?Falta el directorio con los .trx}"
ETIQUETA="${2:?Falta la etiqueta del paso}"
MINIMO="${3:?Falta el mínimo exigido de casos}"

# `dotnet test` sobre la solución escribe UN .trx por ensamblado. Cero ficheros significa
# que no se ejecutó ningún ensamblado: es el caso 3, y es un fallo, no un cero.
FICHEROS=()
while IFS= read -r encontrado; do
  FICHEROS+=("$encontrado")
done < <(find "$DIRECTORIO" -maxdepth 1 -name '*.trx' -print 2>/dev/null | sort)

if [ "${#FICHEROS[@]}" -eq 0 ]; then
  echo "::error title=${ETIQUETA}::No hay ningún .trx en ${DIRECTORIO}: no se ha ejecutado ningún ensamblado de test."
  exit 1
fi

total=0
correctos=0
con_error=0
omitidos=0
desglose=""

# Un atributo del <Counters>. `sed -n .../p` en vez de `grep`: sin coincidencia devuelve
# vacío con código 0, y con `pipefail` un `grep` mudo tumbaría el script.
leer_contador() {
  echo "$2" | sed -n "s/.*[^A-Za-z]$1=\"\([0-9]*\)\".*/\1/p" | head -1
}

for fichero in "${FICHEROS[@]}"; do
  contadores=$(grep -o '<Counters[^/]*/>' "$fichero" | head -1 || true)
  if [ -z "$contadores" ]; then
    echo "::error title=${ETIQUETA}::${fichero} no tiene <Counters>: el .trx está truncado o el formato ha cambiado."
    exit 1
  fi

  t=$(leer_contador total "$contadores")
  p=$(leer_contador passed "$contadores")
  f=$(leer_contador failed "$contadores")
  n=$(leer_contador notExecuted "$contadores")

  # Nombre del ensamblado. Primero del `codeBase` de cualquier resultado: los del runner
  # usan `/` y los de una máquina Windows `\`, se normalizan los dos.
  ensamblado=$(grep -o 'codeBase="[^"]*"' "$fichero" | head -1 | sed 's/.*[\\/]//; s/"$//' || true)

  # Un .trx de CERO casos no tiene ningún `codeBase` —no hay resultados de los que
  # sacarlo—, y es justo el caso en el que más falta hace saber QUÉ ensamblado se quedó a
  # cero. El adaptador de xUnit deja su nombre en la salida capturada, y ese texto lo
  # escribe xUnit, no el CLI: no viene traducido.
  if [ -z "$ensamblado" ]; then
    descubierto=$(grep -o 'Discovering: *[A-Za-z0-9._]*' "$fichero" | head -1 | sed 's/.*: *//' || true)
    [ -n "$descubierto" ] && ensamblado="${descubierto}.dll"
  fi

  [ -n "$ensamblado" ] || ensamblado="(ensamblado desconocido)"

  total=$((total + t))
  correctos=$((correctos + p))
  con_error=$((con_error + f))
  omitidos=$((omitidos + n))
  desglose="${desglose}${desglose:+, }${ensamblado} ${t}"
done

# ------------------------------------------------------------------- QUÉ falló
#
# Por el mismo motivo que existe el recuento: los REGISTROS de un job devuelven 403 sin
# autenticar, así que desde fuera un rojo de test se ve como «Process completed with exit
# code 1» y punto. Sin el nombre del caso ni el mensaje, diagnosticar obliga a tener
# credenciales o a adivinar. Las anotaciones sí son públicas, así que cada caso en rojo
# sale por aquí con su nombre y su aserción.
#
# El .trx pone TODOS los atributos de `<UnitTestResult>` en una línea —incluido
# `outcome`— y el mensaje dentro de `<Message>`, que sí puede ocupar varias. De ahí la
# pequeña máquina de estados de awk en vez de un `grep`.
TOPE_DE_FALLOS=15

if [ "$con_error" -gt 0 ]; then
  for fichero in "${FICHEROS[@]}"; do
    awk -v etiqueta="$ETIQUETA" -v tope="$TOPE_DE_FALLOS" '
      function limpiar(t) {
        gsub(/&lt;/, "<", t); gsub(/&gt;/, ">", t); gsub(/&quot;/, "\"", t)
        gsub(/&#x[0-9A-Fa-f]+;/, " ", t); gsub(/&amp;/, "\\&", t)
        gsub(/[[:space:]]+/, " ", t)
        return t
      }
      function emitir(   m) {
        emitidos++
        if (emitidos > tope) { return }
        m = limpiar(mensaje)
        # 400 cortaba el mensaje justo antes de lo que hacía falta: el cuerpo de la respuesta
        # ya se come casi todo, y lo que el test adjunta detrás —el registro del servidor, que
        # es lo ÚNICO que explica un 500 en la CI— se perdía entero. Ver `RegistroDeFallos`.
        if (length(m) > 1600) { m = substr(m, 1, 1600) " […]" }
        printf "::error title=%s::%s — %s\n", etiqueta, limpiar(nombre), m
      }
      /<UnitTestResult / {
        enFallo = (index($0, "outcome=\"Failed\"") > 0)
        if (enFallo) {
          match($0, /testName="[^"]*"/)
          nombre = substr($0, RSTART + 10, RLENGTH - 11)
          mensaje = ""; enMensaje = 0
        }
        next
      }
      enFallo && enMensaje == 0 && index($0, "<Message>") {
        mensaje = $0
        sub(/^.*<Message>/, "", mensaje)
        enMensaje = 1
        if (index(mensaje, "</Message>")) {
          sub(/<\/Message>.*$/, "", mensaje)
          emitir(); enFallo = 0; enMensaje = 0
        }
        next
      }
      enFallo && enMensaje == 1 {
        linea = $0
        if (index(linea, "</Message>")) {
          sub(/<\/Message>.*$/, "", linea)
          mensaje = mensaje " " linea
          emitir(); enFallo = 0; enMensaje = 0
        } else {
          mensaje = mensaje " " linea
        }
        next
      }
      END {
        if (emitidos > tope) {
          printf "::notice title=%s::y %d caso(s) en rojo más, no listados.\n", etiqueta, emitidos - tope
        }
      }
    ' "$fichero"
  done
fi

RESUMEN="${ETIQUETA}: ${total} casos (${correctos} correctos, ${con_error} con error, ${omitidos} omitidos) en ${#FICHEROS[@]} ensamblados — ${desglose}"

echo "$RESUMEN"

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### ${ETIQUETA}: ${total} casos"
    echo
    echo "| Ensamblado | Casos |"
    echo "|---|---:|"
    echo "$desglose" | tr ',' '\n' | while read -r fila; do
      echo "| \`${fila% *}\` | ${fila##* } |"
    done
    echo "| **Total** | **${total}** |"
  } >> "$GITHUB_STEP_SUMMARY"
fi

if [ "$total" -lt "$MINIMO" ]; then
  echo "::error title=${ETIQUETA}::Se han ejecutado ${total} casos y se exigían al menos ${MINIMO}. Un filtro que no casa con nada, o un ensamblado que ya no se encuentra, salen con código 0 igual que un verde."
  exit 1
fi

# Si hay casos en rojo, el script sale en rojo. Así el paso de la CI puede ejecutarlo
# SIEMPRE —también cuando `dotnet test` ya ha fallado— sin tener que decidir él el
# desenlace: el recuento es lo último que habla, y habla con las anotaciones puestas.
if [ "$con_error" -gt 0 ]; then
  echo "::error title=${ETIQUETA}::${con_error} de ${total} casos en rojo. Los nombres y las aserciones van arriba, como anotaciones."
  exit 1
fi

echo "::notice title=${ETIQUETA}::${RESUMEN}"
