#!/usr/bin/env bash
# El canal de la consola del frontal, con su centinela (ADR-0060) y todos los avisos de React
# (ADR-0066).
#
# Lee el registro de `npm run test` y decide tres cosas:
#
#   1. Que el canal conoce todos los avisos de `act()` que escribe el React instalado, y solo los
#      que escribe. Cada aviso de abajo es un trozo de su mensaje, y se busca en los paquetes de
#      desarrollo de `react` y `react-dom`: una letra cambiada aquí no lo encontraría, y un aviso
#      nuevo de React, al subir de versión, no tendría con qué contarse. Las dos cosas son un rojo,
#      porque las dos dejarían el canal sordo para ese aviso.
#   2. Que la marca del caso centinela (`frontend/src/app/ElCanalDeLaConsola.test.ts`) está. El
#      caso la escribe con `console.error` desde un caso en verde, que es el camino de un aviso de
#      `act()`. Si no sale, el informe se está callando la consola de los casos en verde, y el cero
#      de abajo no mide nada (ADR-0058).
#   3. Que no hay ni un solo aviso de `act()`. El fondo está a cero desde el 1.8 (`AGENTS.md`), así
#      que uno es un hallazgo, y no se cuenta contra ningún umbral.
#
# El registro va a un fichero y `grep` lee el fichero: un `grep -q` al final de una tubería, bajo
# `pipefail`, corta la tubería y da rojos por azar en Linux (ítem 1.12).
#
# Uso: bash scripts/ci/canal-de-la-consola.sh <registro de npm run test>
set -euo pipefail

REGISTRO="${1:?uso: canal-de-la-consola.sh <registro de npm run test>}"
MODULOS="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/frontend/node_modules"

# La misma cadena que escribe el caso centinela. Si dejan de coincidir, la marca no se encuentra y
# el paso falla.
MARCA='CENTINELA DEL CANAL DE LA CONSOLA'

# Los avisos de `act()` de React 19, cada uno por un trozo de la primera línea de su mensaje, que
# es la que llega entera a una línea del registro. El primero lo comparten dos: el de un cambio de
# estado y el de un recurso suspendido que acaba de cargar, los dos fuera de `act()`.
AVISOS=(
  'not wrapped in act'
  'suspended inside an `act` scope'
  'overlapping act() calls'
  'act(async () => ...) without await'
  'not configured to support act'
  'does not have a MessageChannel implementation'
  '`ReactDOMTestUtils.act` is deprecated'
)

# Las cadenas de los paquetes de desarrollo que nombran `act`: lo que React puede escribir sobre él.
mapfile -t DE_REACT < <(
  grep -h -o -E '"[^"]*\bact\b[^"]*"' \
    "$MODULOS"/react/cjs/*.development.js "$MODULOS"/react-dom/cjs/*.development.js | sort -u
)

if [ "${#DE_REACT[@]}" -eq 0 ]; then
  echo "::error title=Canal de la consola::No hay ninguna cadena sobre act en los paquetes de" \
    "desarrollo de $MODULOS, así que no se puede comprobar qué avisos conoce el canal. ¿Falta" \
    "npm ci, o han cambiado de sitio los paquetes de React? (ADR-0066)"
  exit 1
fi

SORDO=0
for AVISO in "${AVISOS[@]}"; do
  ESTA=0
  for CADENA in "${DE_REACT[@]}"; do
    case "$CADENA" in *"$AVISO"*) ESTA=1 ;; esac
  done
  if [ "$ESTA" -eq 0 ]; then
    echo "::error title=Canal de la consola::El canal busca «$AVISO», y el React instalado no" \
      "escribe nada así. Contarlo no vería nunca nada. (ADR-0066)"
    SORDO=1
  fi
done
for CADENA in "${DE_REACT[@]}"; do
  CONTADA=0
  for AVISO in "${AVISOS[@]}"; do
    case "$CADENA" in *"$AVISO"*) CONTADA=1 ;; esac
  done
  if [ "$CONTADA" -eq 0 ]; then
    echo "::error title=Canal de la consola::React escribe ${CADENA:0:120}, y el canal no lo" \
      "cuenta. Su trozo va en AVISOS. (ADR-0066)"
    SORDO=1
  fi
done
if [ "$SORDO" -ne 0 ]; then
  exit 1
fi

if [ ! -s "$REGISTRO" ]; then
  echo "::error title=Canal de la consola::No hay registro de los tests en $REGISTRO, o está vacío."
  exit 1
fi

# `grep -c` sale con 1 cuando cuenta cero, y aquí cero es una respuesta, no un fallo.
MARCAS="$(grep -c -F -- "$MARCA" "$REGISTRO" || true)"
LISTA="$(printf '%s\n' "${AVISOS[@]}")"
CONTADOS="$(grep -c -F -e "$LISTA" -- "$REGISTRO" || true)"

if [ "$MARCAS" -eq 0 ]; then
  echo "::error title=Canal de la consola::El canal está mudo: la marca del caso centinela no" \
    "sale en el registro, así que tampoco saldría un aviso de act(). ¿Se ha quitado el informe" \
    "fijado de frontend/vite.config.ts, o se ha pasado un --reporter? (ADR-0058, ADR-0060)"
  exit 1
fi

if [ "$CONTADOS" -ne 0 ]; then
  echo "::error title=Canal de la consola::Avisos de act() en el registro: $CONTADOS. El fondo" \
    "está a cero desde el 1.8, así que cada uno es un hallazgo: qué efecto se quedó fuera de un" \
    "act(), o qué aserción se hizo sobre algo que todavía no había ocurrido."
  grep -n -F -m 5 -e "$LISTA" -- "$REGISTRO"
  exit 1
fi

echo "::notice title=Canal de la consola::El canal está abierto, con la marca en el registro" \
  "($MARCAS), y no hay ningún aviso de act(): ninguno de los ${#AVISOS[@]} trozos, que cubren" \
  "las ${#DE_REACT[@]} cadenas de React sobre act."
