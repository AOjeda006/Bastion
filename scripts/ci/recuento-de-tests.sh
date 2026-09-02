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
# POR QUÉ NO BASTA CON EL MÍNIMO DE CASOS
# ---------------------------------------
# Porque un suelo sobre el TOTAL protege cada vez menos según crece la suite. Con 403 casos
# repartidos en cinco ensamblados, perder uno entero deja entre 292 y 388: un suelo de 300
# caza dos de los cinco y los otros tres pasan de largo. El carril de integración estaba
# peor todavía —206 casos con suelo de 100—: no cazaba ninguno.
#
# La defensa que corresponde es la que usa el resto del proyecto para todo lo que se
# descubre: comparar la LISTA ENTERA contra la declarada. Un ensamblado que desaparece se
# nombra; uno que aparece obliga a declararlo. Un número que baja no dice cuál faltaba, y
# es justo lo primero que hace falta saber.
#
# Uso: recuento-de-tests.sh <directorio-con-trx> <etiqueta> <mínimo-exigido> <ensamblados>
#
#   <ensamblados>  lista separada por comas de los `.dll` que TIENEN que haber corrido,
#                  p. ej. "Bastion.Arquitectura.Tests.dll,Bastion.Identidad.UnitTests.dll".

set -euo pipefail

DIRECTORIO="${1:?Falta el directorio con los .trx}"
ETIQUETA="${2:?Falta la etiqueta del paso}"
MINIMO="${3:?Falta el mínimo exigido de casos}"
# Obligatorio, no opcional: un carril nuevo que se olvidara de declarar sus ensamblados
# volvería a quedarse con el suelo de casos como única defensa, que es de donde venimos.
ESPERADOS="${4:?Falta la lista de ensamblados que tienen que haber corrido}"

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
ENCONTRADOS=()

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

  # Solo cuenta como «ha corrido» el que ha ejecutado AL MENOS UN caso. Un ensamblado
  # visitado que no ejecuta nada —el filtro dejó de casar, la categoría se cayó, los casos
  # se borraron— deja un .trx de cero casos que por su nombre es indistinguible de uno sano.
  # De paso, esto hace que la lista declarada no dependa de si `dotnet test` escribe o no un
  # .trx para los ensamblados que el filtro descarta enteros: en el carril rápido aparecen
  # dos de esos, y que aparezcan o no no tiene por qué mover la declaración.
  if [ "${t:-0}" -gt 0 ]; then
    ENCONTRADOS+=("$ensamblado")
  fi
done

# ------------------------------------------------- QUÉ ensamblados han corrido
#
# La lista entera, comparada en las dos direcciones. Que FALTE uno es la avería que el
# suelo de casos no ve: el ensamblado deja de compilarse, deja de encontrarse o se le cae
# la categoría, y el total baja lo justo para seguir por encima del mínimo. Que SOBRE uno
# no es una avería, pero obliga a declararlo: si no, el carril crece sin que nadie decida
# que ha crecido, y la lista deja de ser una afirmación para volver a ser un comentario.
LISTA_ESPERADA=$(mktemp)
LISTA_ENCONTRADA=$(mktemp)
trap 'rm -f "$LISTA_ESPERADA" "$LISTA_ENCONTRADA"' EXIT

printf '%s' "$ESPERADOS" | tr ',' '\n' | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' \
  | grep -v '^$' | LC_ALL=C sort -u > "$LISTA_ESPERADA"

# Por ficheros y no por sustitución de procesos: con la lista vacía —que es el caso en el
# que más falta hace que esto funcione— un `printf` de un array vacío escribe una línea en
# blanco, y `comm` la trataría como un ensamblado llamado «».
: > "$LISTA_ENCONTRADA"
if [ "${#ENCONTRADOS[@]}" -gt 0 ]; then
  printf '%s\n' "${ENCONTRADOS[@]}" | LC_ALL=C sort -u > "$LISTA_ENCONTRADA"
fi

faltan=$(LC_ALL=C comm -23 "$LISTA_ESPERADA" "$LISTA_ENCONTRADA" | tr '\n' ' ')
sobran=$(LC_ALL=C comm -13 "$LISTA_ESPERADA" "$LISTA_ENCONTRADA" | tr '\n' ' ')
encontrados=$(tr '\n' ' ' < "$LISTA_ENCONTRADA")

incompleto=0

if [ -n "${faltan// /}" ]; then
  echo "::error title=${ETIQUETA}::No ha corrido ningún caso de: ${faltan}. Un ensamblado que ya no se encuentra, o cuyos casos han dejado de casar con el filtro, sale con código 0 y solo baja el total."
  incompleto=1
fi

if [ -n "${sobran// /}" ]; then
  echo "::error title=${ETIQUETA}::Han corrido ensamblados no declarados: ${sobran}. Añádelos a la lista del paso en el workflow para que a partir de ahora se exija que corran."
  incompleto=1
fi

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

# Va DESPUÉS del resumen a propósito: cuando esto falla, lo primero que se quiere ver es el
# desglose por ensamblado, y las anotaciones con los nombres ya están emitidas más arriba.
if [ "$incompleto" -ne 0 ]; then
  echo "::error title=${ETIQUETA}::Los ensamblados que han corrido no son los declarados. Han corrido: ${encontrados}."
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
