#!/usr/bin/env bash
#
# Comprueba que el modelo de EF Core y sus migraciones NO han divergido.
#
# POR QUÉ EXISTE
# --------------
# Cambiar una entidad y olvidar generar la migración no rompe nada en local —el modelo manda
# en memoria— y no rompe nada en los tests si se crea el esquema desde el modelo. Rompe en el
# despliegue, contra una base de datos que se quedó con el esquema anterior: un despliegue roto
# en diferido (§14). Aquí se detecta en segundos.
#
# `has-pending-model-changes` NO se conecta a ninguna base de datos: compara el modelo con la
# instantánea de la última migración. Por eso este paso funciona en la CI sin PostgreSQL.
#
# UN MÓDULO, UNA LÍNEA. Cada módulo tiene su DbContext y su propia cadena de migraciones, así
# que se comprueban de uno en uno. Al añadir un módulo con persistencia, se añade su línea.

set -euo pipefail

STARTUP="src/Api"
FALLOS=0

comprobar() {
  modulo="$1"
  proyecto="src/Modules/${modulo}/Bastion.${modulo}.Infrastructure"

  if [ ! -d "$proyecto" ]; then
    echo "::error::No existe $proyecto: revisa la lista de módulos de este script."
    FALLOS=$((FALLOS + 1))
    return
  fi

  if dotnet ef migrations has-pending-model-changes \
      --project "$proyecto" --startup-project "$STARTUP" --no-build >/dev/null 2>&1; then
    echo "::error title=Migraciones::El modelo de ${modulo} tiene cambios sin migrar. Genera la migración: dotnet ef migrations add <Nombre> --project ${proyecto} --startup-project ${STARTUP} --output-dir ../../../../db/migraciones/${modulo}"
    FALLOS=$((FALLOS + 1))
  else
    echo "${modulo}: el modelo y sus migraciones coinciden."
  fi
}

# `has-pending-model-changes` sale con 0 CUANDO HAY cambios pendientes y con 1 cuando no los
# hay, que es al revés de lo que uno espera de un comando de comprobación. De ahí que el `if`
# de arriba trate el éxito como el fallo.
for modulo in Organizacion; do
  comprobar "$modulo"
done

if [ "$FALLOS" -gt 0 ]; then
  exit 1
fi

echo "::notice title=Migraciones::Modelo y migraciones coinciden en todos los módulos con persistencia."
