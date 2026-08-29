#!/usr/bin/env bash
#
# Comprueba que las migraciones de cada módulo EXISTEN en su ensamblado y que el modelo de
# EF Core no ha divergido de ellas.
#
# POR QUÉ EXISTE
# --------------
# Dos fallos distintos, los dos silenciosos, y los dos rompen en el despliegue y no en local:
#
#   1. La migración no está compilada. Las migraciones de cada módulo viven FUERA del proyecto,
#      en `db/migraciones/<Modulo>` (§14), así que el glob por defecto del SDK no las recoge:
#      basta con olvidar su `<Compile Include>` para que EF no vea ninguna. Entonces `Migrate()`
#      aplica cero migraciones y crea cero tablas, sin error y sin aviso.
#
#   2. El modelo cambió y nadie generó la migración. En local no rompe nada —el modelo manda en
#      memoria—; rompe contra una base de datos que se quedó con el esquema anterior.
#
# La comprobación 1 no es redundante con la 2: `has-pending-model-changes` compara el modelo con
# la INSTANTÁNEA (`Migrations/…ModelSnapshot.cs`), que sí está dentro del proyecto y sí se
# compila. Con las migraciones fuera del ensamblado, modelo e instantánea siguen coincidiendo y
# la comprobación 2 da verde sobre una base de datos vacía. Se pregunta por el EFECTO —¿cuántas
# migraciones ve EF?— y no por la configuración.
#
# Ninguno de los dos comandos se conecta a base de datos: este paso funciona sin PostgreSQL.
#
# UN MÓDULO, UNA LÍNEA. Cada módulo tiene su DbContext y su propia cadena de migraciones. Al
# añadir un módulo con persistencia, se añade su nombre al bucle del final.

set -euo pipefail

STARTUP="src/Api"
FALLOS=0

# `dotnet ef --no-build` busca el ensamblado de la configuración que se le diga, y por defecto
# eso es Debug. La CI compila en Release, así que allí no encontraría nada y el paso fallaría
# por un motivo que no tiene nada que ver con las migraciones. Se pasa por entorno.
CONFIGURACION="${CONFIGURACION:-Debug}"

# `dotnet ef` construye el host de src/Api para descubrir los DbContext, y ese host EXIGE las tres
# variables del JWT: sin ellas no arranca, y con razón (un secreto con valor por omisión es un
# secreto conocido). Aquí se generan al vuelo y no se escriben en ninguna parte: esta clave no
# firma nada, solo hace que el host llegue a construirse. Ninguna de las dos comprobaciones se
# conecta a base de datos ni emite un token.
export JWT_ISSUER="bastion-comprobacion-de-migraciones"
export JWT_AUDIENCE="bastion-comprobacion-de-migraciones"
JWT_SIGNING_KEY="$(head -c 48 /dev/urandom | base64 | tr -d '
')"
export JWT_SIGNING_KEY

comprobar() {
  modulo="$1"
  proyecto="src/Modules/${modulo}/Bastion.${modulo}.Infrastructure"

  # Desde que hay más de un DbContext en la solución, `dotnet ef` no adivina cuál: sin --context
  # falla con «More than one DbContext was found» y el paso se caería por un motivo que no tiene
  # nada que ver con las migraciones.
  contexto="${modulo}DbContext"

  if [ ! -d "$proyecto" ]; then
    echo "::error title=Migraciones::No existe $proyecto: revisa la lista de módulos de este script."
    FALLOS=$((FALLOS + 1))
    return
  fi

  # Los nombres de migración empiezan por la marca de tiempo de catorce dígitos que genera EF.
  # Contar esas líneas descarta los avisos que el comando escribe por lo demás.
  migraciones=$(dotnet ef migrations list --project "$proyecto" --startup-project "$STARTUP" \
      --context "$contexto" --configuration "$CONFIGURACION" --no-build --no-connect 2>/dev/null \
      | grep -c '^[0-9]\{14\}_' || true)

  if [ "${migraciones:-0}" -eq 0 ]; then
    echo "::error title=Migraciones::EF no encuentra NINGUNA migración de ${modulo} en el ensamblado. Viven en db/migraciones/${modulo} y hay que incluirlas explícitamente en ${proyecto}: <Compile Include=\"../../../../db/migraciones/${modulo}/**/*.cs\" />"
    FALLOS=$((FALLOS + 1))
    return
  fi

  # `has-pending-model-changes` sale con 0 cuando NO hay cambios pendientes y con 1 cuando sí
  # los hay. Comprobado por el efecto —añadiendo una propiedad de sombra al modelo y viendo el
  # código de salida pasar de 0 a 1—, no por lo que pareciera razonable.
  if dotnet ef migrations has-pending-model-changes --project "$proyecto" --startup-project "$STARTUP" \
      --context "$contexto" --configuration "$CONFIGURACION" --no-build >/dev/null 2>&1; then
    echo "${modulo}: ${migraciones} migración(es) en el ensamblado, y el modelo coincide con ellas."
  else
    echo "::error title=Migraciones::El modelo de ${modulo} tiene cambios sin migrar. Genera la migración: dotnet ef migrations add <Nombre> --project ${proyecto} --startup-project ${STARTUP} --context ${contexto} --output-dir ../../../../db/migraciones/${modulo}"
    FALLOS=$((FALLOS + 1))
  fi
}

for modulo in Auditoria Organizacion Identidad; do
  comprobar "$modulo"
done

if [ "$FALLOS" -gt 0 ]; then
  exit 1
fi

echo "::notice title=Migraciones::Modelo y migraciones coinciden en todos los módulos con persistencia."
