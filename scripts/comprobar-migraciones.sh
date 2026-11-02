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
# añadir un módulo con persistencia, se añade su nombre a MODULOS, al final; y si no se añade, el
# guion lo dice, porque esa lista se compara con los contextos que hay en el disco.

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

  # «`dotnet ef` NO ARRANCA» NO ES «NO HAY MIGRACIONES». Hasta el epílogo del 2.9 la salida de
  # error iba a /dev/null y el código de salida se perdía en la tubería, así que una herramienta
  # sin restaurar, un ensamblado sin compilar o un host que no se construye salían como «EF no
  # encuentra NINGUNA migración»: el diagnóstico más caro de creer, porque manda a revisar el
  # proyecto. Ahora la orden se ejecuta sola, con las dos salidas juntas, y se mira su código
  # ANTES de contar nada. Si no es cero, el rojo lleva la salida entera a la vista.
  if salida=$(dotnet ef migrations list --project "$proyecto" --startup-project "$STARTUP" \
      --context "$contexto" --configuration "$CONFIGURACION" --no-build --no-connect 2>&1); then
    codigo=0
  else
    codigo=$?
  fi

  if [ "$codigo" -ne 0 ]; then
    echo "::error title=Migraciones::dotnet ef no arranca para ${modulo} (sale con ${codigo}): no se sabe si hay migraciones. Su salida, entera:"
    printf '%s\n' "$salida"
    FALLOS=$((FALLOS + 1))
    return
  fi

  # Los nombres de migración empiezan por la marca de tiempo de catorce dígitos que genera EF.
  # Contar esas líneas descarta los avisos que el comando escribe por lo demás. Se cuenta sobre la
  # variable, sin tubería: `grep -c` sale con 1 cuando cuenta cero, y eso no es un fallo.
  migraciones=$(grep -c '^[0-9]\{14\}_' <<< "$salida" || true)

  if [ "${migraciones:-0}" -eq 0 ]; then
    echo "::error title=Migraciones::EF no encuentra NINGUNA migración de ${modulo} en el ensamblado. Viven en db/migraciones/${modulo} y hay que incluirlas explícitamente en ${proyecto}: <Compile Include=\"../../../../db/migraciones/${modulo}/**/*.cs\" />"
    FALLOS=$((FALLOS + 1))
    return
  fi

  # `has-pending-model-changes` sale con 0 cuando NO hay cambios pendientes y con 1 cuando sí
  # los hay. Comprobado por el efecto —añadiendo una propiedad de sombra al modelo y viendo el
  # código de salida pasar de 0 a 1—, no por lo que pareciera razonable. La orden de arriba ya
  # demostró que la herramienta arranca con estos mismos argumentos, así que un 1 aquí es, casi
  # seguro, un modelo sin migrar. El «casi» es la razón de que su salida tampoco se tire: si
  # fuera otra cosa, se lee en el registro.
  if pendientes=$(dotnet ef migrations has-pending-model-changes --project "$proyecto" \
      --startup-project "$STARTUP" --context "$contexto" --configuration "$CONFIGURACION" \
      --no-build 2>&1); then
    echo "${modulo}: ${migraciones} migración(es) en el ensamblado, y el modelo coincide con ellas."
  else
    echo "::error title=Migraciones::El modelo de ${modulo} tiene cambios sin migrar. Genera la migración: dotnet ef migrations add <Nombre> --project ${proyecto} --startup-project ${STARTUP} --context ${contexto} --output-dir ../../../../db/migraciones/${modulo}"
    printf '%s\n' "$pendientes"
    FALLOS=$((FALLOS + 1))
  fi
}

# LA LISTA SE COMPARA CON EL DISCO, entera y en los dos sentidos. Una lista tecleada que nadie
# contrasta es un selector: el módulo que se queda fuera no se mira, y el guion sale en verde. En el
# 3.3, quitar Compras de aquí lo dejó en 0 (la mutación 1065). El universo es «los módulos con un
# contexto propio», el fichero <Modulo>DbContext.cs de su Persistencia, que es lo que necesita
# `dotnet ef` para mirarlo. Que el patrón encuentre alguno se afirma aparte, para que un patrón
# roto diga eso y no «la lista no coincide».
MODULOS=(Auditoria Organizacion Identidad Terceros Catalogo Inventario Compras)

en_disco=$(for fichero in src/Modules/*/Bastion.*.Infrastructure/Persistencia/*DbContext.cs; do
  if [ -f "$fichero" ]; then basename "$fichero" DbContext.cs; fi
done | LC_ALL=C sort)
declarados=$(printf '%s\n' "${MODULOS[@]}" | LC_ALL=C sort)

if [ -z "$en_disco" ]; then
  echo "::error title=Migraciones::No hay ningún contexto de módulo en src/Modules/*/Bastion.*.Infrastructure/Persistencia: el patrón de este guion no mira donde están."
  FALLOS=$((FALLOS + 1))
elif [ "$en_disco" != "$declarados" ]; then
  echo "::error title=Migraciones::La lista MODULOS de este guion no es la de los contextos que hay en el disco. En la lista: $(tr '\n' ' ' <<< "$declarados"). En el disco: $(tr '\n' ' ' <<< "$en_disco")."
  FALLOS=$((FALLOS + 1))
fi

for modulo in "${MODULOS[@]}"; do
  comprobar "$modulo"
done

if [ "$FALLOS" -gt 0 ]; then
  exit 1
fi

echo "::notice title=Migraciones::Modelo y migraciones coinciden en todos los módulos con persistencia."
