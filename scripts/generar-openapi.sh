#!/usr/bin/env bash
#
# Genera `docs/api/openapi.json` desde el host de la API, y con `--comprobar` se limita a
# comprobar que el fichero versionado sigue siendo el que saldría hoy.
#
# POR QUÉ EXISTE
# --------------
# El documento OpenAPI es el contrato del que se genera el cliente de TypeScript del frontal.
# Si se escribiera a mano habría dos fuentes de verdad; si se generase pero nadie comprobara que
# está al día, habría dos fuentes de verdad con un paso extra en medio — que es peor, porque la
# segunda parece derivada de la primera y no lo está.
#
# Así que se versiona Y se comprueba. Es la misma forma que `comprobar-migraciones.sh`: el
# artefacto está en el repositorio, se ve en el `diff` de la revisión, y la CI vuelve a generarlo
# y falla si difiere. Un paso, sin encadenar trabajos ni pasarse artefactos entre ellos.
#
# QUÉ HACE FALTA PARA GENERARLO
# -----------------------------
# `Microsoft.Extensions.ApiDescription.Server` construye el host de `src/Api` para preguntarle por
# sus descripciones. Construirlo EXIGE las tres variables del JWT: sin ellas no arranca, y con
# razón (un secreto con valor por omisión es un secreto conocido). Se generan al vuelo y no se
# escriben en ninguna parte: esta clave no firma nada, solo hace que el host llegue a construirse.
# No se abre ninguna conexión a base de datos ni se emite ningún token.
#
# El documento NO depende de la configuración de compilación: Debug y Release dan el mismo
# fichero. La variable existe solo para encontrar el ensamblado donde la CI lo deja.

set -euo pipefail

CONFIGURACION="${CONFIGURACION:-Debug}"

PROYECTO="src/Api/Bastion.Api.csproj"
GENERADO="src/Api/obj/openapi/Bastion.Api.json"
VERSIONADO="docs/api/openapi.json"

COMPROBAR=0
if [ "${1:-}" = "--comprobar" ]; then
  COMPROBAR=1
fi

export JWT_ISSUER="bastion-generacion-de-openapi"
export JWT_AUDIENCE="bastion-generacion-de-openapi"
JWT_SIGNING_KEY="$(head -c 48 /dev/urandom | base64 | tr -d '\n')"
export JWT_SIGNING_KEY

# El objetivo `GenerateOpenApiDocuments` NO reconstruye: lee el ensamblado que encuentre. Sin esta
# primera compilación, un cambio en un controlador se quedaría fuera del documento y la
# comprobación daría verde sobre el contrato de ayer. En la CI esta línea no cuesta nada, porque
# el trabajo ya ha compilado la solución.
dotnet build "$PROYECTO" --configuration "$CONFIGURACION" --no-restore --nologo -v q

dotnet build "$PROYECTO" --target:GenerateOpenApiDocuments --configuration "$CONFIGURACION" \
  --no-restore --nologo -v q

if [ ! -f "$GENERADO" ]; then
  echo "::error title=OpenAPI::La generación no ha dejado ningún fichero en ${GENERADO}. ¿Sigue registrado AgregarContratoDeLaApi() en src/Api/Program.cs?"
  exit 1
fi

# Se normaliza al escribir, no al comparar: el fichero versionado tiene que ser byte a byte el
# mismo se genere en Windows o en Linux. Fines de línea LF —lo que dice `.gitattributes`— y un
# salto final, que la herramienta no pone y que todo lo demás del repositorio lleva.
normalizar() {
  tr -d '\r' < "$GENERADO" > "$1"

  if [ -n "$(tail -c 1 "$1")" ]; then
    printf '\n' >> "$1"
  fi
}

if [ "$COMPROBAR" -eq 0 ]; then
  normalizar "$VERSIONADO"
  echo "::notice title=OpenAPI::Documento regenerado en ${VERSIONADO}."
  exit 0
fi

if [ ! -f "$VERSIONADO" ]; then
  echo "::error title=OpenAPI::No existe ${VERSIONADO}. Genéralo con: bash scripts/generar-openapi.sh"
  exit 1
fi

RECIEN="$(mktemp)"
trap 'rm -f "$RECIEN"' EXIT

normalizar "$RECIEN"

if diff -u "$VERSIONADO" "$RECIEN" > /dev/null 2>&1; then
  operaciones=$(grep -c '"operationId"' "$VERSIONADO" || true)
  echo "::notice title=OpenAPI::El documento versionado está al día: ${operaciones} operaciones."
  exit 0
fi

echo "::error title=OpenAPI::El contrato ha cambiado y ${VERSIONADO} se ha quedado atrás. Regenéralo y commitéalo: bash scripts/generar-openapi.sh"
echo "--- diferencias (versionado vs. recién generado) ---"
diff -u "$VERSIONADO" "$RECIEN" | head -60
exit 1
