#!/usr/bin/env bash
#
# Genera `docs/api/errores.json` —el catálogo de los `type` que la API puede emitir— desde el
# código fuente, y con `--comprobar` se limita a comprobar que el fichero versionado sigue siendo
# el que saldría hoy.
#
# POR QUÉ EXISTE
# --------------
# Desde el ADR-0030, el texto que lee una persona cuando falla una validación lo escribe el
# FRONTAL, mapeando el `type` estable del ProblemDetails. Eso convierte el conjunto de `type`
# emitibles en contrato: si el backend estrena un código y el frontal no tiene texto para él, el
# usuario ve el mensaje genérico y nadie se entera. La única forma de que eso sea rojo el día que
# se escribe es que el conjunto viaje al frontal como un artefacto, y que alguien lo compare
# contra los diccionarios.
#
# Es la MISMA forma que `generar-openapi.sh` y `comprobar-migraciones.sh`, a propósito: el
# artefacto está en el repositorio, se ve en el `diff` de la revisión, y la CI vuelve a generarlo
# y falla si difiere. Un paso, sin encadenar trabajos ni pasarse artefactos entre ellos.
#
# DE DÓNDE SALE EL CONJUNTO
# -------------------------
# Del código fuente, no de un ensamblado. Los códigos son literales dentro de llamadas a
# `ErrorDeOperacion.<Clase>(...)`, así que no hay reflexión que los alcance: un método de fábrica
# hay que INVOCARLO para que devuelva su código, y la mitad de las llamadas están en línea dentro
# de un caso de uso. Se barre el texto.
#
# Un barrido de texto tiene un modo de fallo propio y conocido: dejar de casar y devolver menos
# sin decirlo. Contra eso, tres cosas, y las tres son ERRORES y no avisos:
#
#   1. Todo sitio de llamada tiene que resolverse a un código. Si el primer argumento no es un
#      literal ni una constante del mismo fichero, el guion PARA y lo nombra. Nunca se salta uno.
#   2. Los ficheros ancla tienen que aportar. Si un `glob` deja de encontrarlos —una carpeta que se
#      mueve, un renombrado— el barrido daría cero y verde, que es el falso verde de siempre.
#   3. Un mismo código con dos clases distintas para el guion. El código es contrato publicado; si
#      un día sale 400 y otro 409, quien ramifica sobre él ya no puede.
#
# Lo que este guion NO comprueba es que cada `type` tenga texto: eso es del frontal y lo comprueba
# el barrido de diccionarios (`ElCambioDeIdioma.test.tsx`), que compara este artefacto contra las
# entradas de cada idioma, entero y en los dos sentidos.

set -euo pipefail

VERSIONADO="docs/api/errores.json"

# Los ficheros que TIENEN que aportar códigos, pase lo que pase con el resto del barrido. Son el
# ancla contra el falso verde: se eligen dos de sitios distintos —uno de BuildingBlocks y uno de
# módulo— para que mover una carpeta no los apague a los dos a la vez.
ANCLAS="src/BuildingBlocks/Application/Concurrencia/ErroresDeConcurrencia.cs src/Modules/Organizacion/Bastion.Organizacion.Application/Empresas/ErroresDeEmpresa.cs"

COMPROBAR=0
if [ "${1:-}" = "--comprobar" ]; then
  COMPROBAR=1
fi

# El barrido. Escribe una línea `codigo<TAB>clase<TAB>fichero:linea` por sitio de llamada, y
# cualquier cosa que no sepa resolver la escribe en el descriptor 2 con la palabra SINRESOLVER.
#
# Se hace en dos pasadas sobre cada fichero porque una constante puede estar declarada DESPUÉS de
# usarse: primero se recogen las `const string`, y solo entonces se resuelven las llamadas.
barrer() {
  find src -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0 |
    xargs -0 awk '
      function volcar(  i) {
        for (i = 1; i <= n; i++) {
          arg = args[i]
          if (arg ~ /^"/) {
            codigo = arg
            gsub(/^"|"$/, "", codigo)
          } else if (arg in constantes) {
            codigo = constantes[arg]
          } else {
            print "SINRESOLVER\t" arg "\t" fichero ":" lineas[i] > "/dev/stderr"
            continue
          }
          print codigo "\t" clases[i] "\t" fichero ":" lineas[i]
        }
      }

      FNR == 1 {
        if (NR > 1) volcar()
        delete constantes; delete args; delete clases; delete lineas
        n = 0
        fichero = FILENAME
        # Segunda pasada del mismo fichero: se lee entero aparte para tener las constantes antes
        # de resolver nada.
        while ((getline linea < FILENAME) > 0) {
          if (match(linea, /const string [A-Za-z_][A-Za-z0-9_]* = "[^"]*"/)) {
            trozo = substr(linea, RSTART, RLENGTH)
            nombre = trozo
            sub(/^const string /, "", nombre)
            sub(/ =.*$/, "", nombre)
            valor = trozo
            sub(/^[^"]*"/, "", valor)
            sub(/"$/, "", valor)
            constantes[nombre] = valor
          }
        }
        close(FILENAME)
      }

      # El fichero que DEFINE ErrorDeOperacion no aporta códigos: sus menciones son la firma de
      # las fábricas, no llamadas.
      FILENAME ~ /ErrorDeOperacion\.cs$/ { next }

      {
        resto = $0
        while (match(resto, /ErrorDeOperacion\.[A-Z][A-Za-z]*\(/)) {
          clase = substr(resto, RSTART, RLENGTH)
          sub(/^ErrorDeOperacion\./, "", clase)
          sub(/\($/, "", clase)
          resto = substr(resto, RSTART + RLENGTH)

          # El primer argumento: en la misma línea si lo hay, y si no en las siguientes. Se
          # buscan hasta cuatro líneas por delante, que es más de lo que el formateador permite.
          arg = resto
          sub(/^[ \t]+/, "", arg)
          adelanto = 0
          while (arg == "" && adelanto < 4) {
            if ((getline arg) <= 0) break
            adelanto++
            sub(/^[ \t]+/, "", arg)
          }

          # `match` + `substr` y no `sub` con retrorreferencia: awk NO tiene grupos de captura en
          # `sub`, y `\1` ahí es el carácter literal. Costó el primer rojo del guion: los 51
          # sitios salieron "sin resolver" con el nombre `\1`.
          if (match(arg, /^"[^"]*"/) || match(arg, /^[A-Za-z_][A-Za-z0-9_]*/)) {
            arg = substr(arg, RSTART, RLENGTH)
          }

          n++
          args[n] = arg
          clases[n] = clase
          lineas[n] = FNR
          if (adelanto > 0) resto = ""
        }
      }

      END { volcar() }
    '
}

CRUDO="$(mktemp)"
FALLOS="$(mktemp)"
trap 'rm -f "$CRUDO" "$FALLOS"' EXIT

barrer > "$CRUDO" 2> "$FALLOS"

if [ -s "$FALLOS" ]; then
  echo "::error title=Catálogo de errores::Hay sitios de llamada a ErrorDeOperacion cuyo código no se puede resolver a un literal ni a una constante del mismo fichero. Un código que el barrido no ve no llega al frontal y su texto no existe: declara el código como literal o como 'const string' en el fichero.%0A%0A$(awk '{ printf "%s en %s%%0A", $2, $3 }' "$FALLOS")"
  exit 1
fi

TOTAL=$(wc -l < "$CRUDO")
if [ "$TOTAL" -eq 0 ]; then
  echo "::error title=Catálogo de errores::El barrido no ha encontrado NI UN código. O se ha renombrado ErrorDeOperacion, o el 'find' ya no encuentra las fuentes. Cero códigos no es un catálogo vacío: es un barrido roto."
  exit 1
fi

for ancla in $ANCLAS; do
  if ! grep -q -F "	$ancla:" "$CRUDO"; then
    echo "::error title=Catálogo de errores::El fichero ancla ${ancla} no ha aportado ningún código. O ha dejado de emitir errores —y entonces hay que cambiar el ancla a conciencia— o el barrido ha dejado de verlo."
    exit 1
  fi
done

# Un código con dos clases es un contrato que cambia de significado según por dónde salga.
CHOQUES="$(sort -u "$CRUDO" | cut -f1,2 | sort -u | cut -f1 | uniq -d || true)"
if [ -n "$CHOQUES" ]; then
  echo "::error title=Catálogo de errores::Estos códigos se emiten con más de una clase de error, así que el mismo 'type' saldría con dos códigos de estado distintos: $(echo "$CHOQUES" | tr '\n' ' ')"
  exit 1
fi

componer() {
  {
    printf '{\n'
    printf '  "base": "/errors/",\n'
    printf '  "tipos": [\n'
    sort -u "$CRUDO" | cut -f1,2 | sort -u |
      awk -F'\t' '{ printf "%s    { \"codigo\": \"%s\", \"type\": \"/errors/%s\", \"clase\": \"%s\" }", (NR > 1 ? ",\n" : ""), $1, $1, $2 } END { printf "\n" }'
    printf '  ]\n'
    printf '}\n'
  } > "$1"
}

if [ "$COMPROBAR" -eq 0 ]; then
  componer "$VERSIONADO"
  echo "::notice title=Catálogo de errores::Catálogo regenerado en ${VERSIONADO}: $(grep -c '"codigo"' "$VERSIONADO") tipos, de ${TOTAL} sitios de llamada."
  exit 0
fi

if [ ! -f "$VERSIONADO" ]; then
  echo "::error title=Catálogo de errores::No existe ${VERSIONADO}. Genéralo con: bash scripts/generar-errores.sh"
  exit 1
fi

RECIEN="$(mktemp)"
trap 'rm -f "$CRUDO" "$FALLOS" "$RECIEN"' EXIT
componer "$RECIEN"

if diff -u "$VERSIONADO" "$RECIEN" > /dev/null 2>&1; then
  echo "::notice title=Catálogo de errores::El catálogo versionado está al día: $(grep -c '"codigo"' "$VERSIONADO") tipos, de ${TOTAL} sitios de llamada."
  exit 0
fi

# El `diff` va DENTRO de la anotación por la misma razón que en `generar-openapi.sh`: los registros
# de un job devuelven 403 sin autenticar y las anotaciones no.
escapar() {
  printf '%s' "$1" | awk '{ gsub(/%/, "%25"); printf "%s%%0A", $0 }'
}

DIFERENCIAS="$(diff -u "$VERSIONADO" "$RECIEN" | head -60 || true)"

echo "::error title=Catálogo de errores::El catálogo de errores ha cambiado y ${VERSIONADO} se ha quedado atrás. Regenéralo y commitéalo: bash scripts/generar-errores.sh — y acuérdate de que cada 'type' nuevo necesita su texto en TODOS los diccionarios del frontal, o el barrido de idiomas se pondrá rojo.%0A%0A$(escapar "$DIFERENCIAS")"
exit 1
