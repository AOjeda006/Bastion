"""Las dependencias de cada ref, como CONJUNTOS, y lo que entra o sale entre la primera y las demás.

Uso:  python scripts/dependencias-por-conjuntos.py <ref-base> [<ref> ...]
      python scripts/dependencias-por-conjuntos.py 7f676e2 HEAD

Lee los ficheros de bloqueo del árbol de cada ref con `git show`, no del disco: lo que se compara es
lo commiteado, y un `npm install` sin commitear no cambia la cifra.

Qué cuenta, dicho una vez para que dos informes se puedan comparar:

- **.NET**: la unión de TODOS los `packages.lock.json`. Un par es `nombre/versión resuelta`, con el
  nombre en minúsculas; las entradas `"type": "Project"` son proyectos de la solución, no paquetes,
  y se cuentan aparte.
- **Frontal**: las entradas de `packages` de `frontend/package-lock.json`, como `ruta@versión`,
  **SIN LA RAÍZ**. La entrada de clave `""` es el propio `bastion-web`, no una dependencia. Esta
  convención se fijó en el ítem 1.14 porque el mismo fichero dio 548 en el 1.9 y 549 en el 1.10 y
  el 1.11 —con y sin raíz, las dos cuentas bien hechas— y hacía buscar un paquete nuevo a quien
  comparaba los dos informes.

La cifra es lo de menos: lo que decide es la diferencia de conjuntos. Un paquete que entra es uno
cuya licencia hay que comprobar antes de adoptarlo (AGENTS.md); una cuenta igual con un paquete
cambiado por otro también lo es, y solo la diferencia lo enseña.
"""
import json
import subprocess
import sys


def git(*argumentos):
    return subprocess.run(
        ["git", *argumentos], capture_output=True, text=True, encoding="utf-8", check=True
    ).stdout


def de_dotnet(ref):
    rutas = [
        ruta
        for ruta in git("ls-tree", "-r", "--name-only", ref).splitlines()
        if ruta.endswith("packages.lock.json")
    ]
    pares, proyectos = set(), set()
    for ruta in rutas:
        for marco in json.loads(git("show", f"{ref}:{ruta}")).get("dependencies", {}).values():
            for nombre, dato in marco.items():
                if dato.get("type") == "Project":
                    proyectos.add(nombre.lower())
                else:
                    pares.add(f"{nombre.lower()}/{dato['resolved']}")
    return rutas, pares, proyectos


def del_frontal(ref):
    paquetes = json.loads(git("show", f"{ref}:frontend/package-lock.json"))["packages"]
    # Sin la raíz: la clave "" es bastion-web.
    return {f"{ruta}@{dato.get('version')}" for ruta, dato in paquetes.items() if ruta}


def main(refs):
    if not refs:
        print(__doc__.splitlines()[2], file=sys.stderr)
        return 2

    datos = {ref: (de_dotnet(ref), del_frontal(ref)) for ref in refs}

    for ref, ((rutas, pares, proyectos), frontal) in datos.items():
        print(
            f"{ref}: {len(rutas)} packages.lock.json · {len(pares)} pares nombre/versión · "
            f"{len(proyectos)} Project · frontal {len(frontal)} entradas sin la raíz"
        )

    (_, pares_base, proyectos_base), frontal_base = datos[refs[0]]
    for ref in refs[1:]:
        (_, pares, proyectos), frontal = datos[ref]
        print(f"{refs[0]} → {ref}:")
        print(f"  pares añadidos    {sorted(pares - pares_base)}")
        print(f"  pares retirados   {sorted(pares_base - pares)}")
        print(f"  Project añadidos  {sorted(proyectos - proyectos_base)}")
        print(f"  Project retirados {sorted(proyectos_base - proyectos)}")
        print(f"  frontal añadidas  {sorted(frontal - frontal_base)}")
        print(f"  frontal retiradas {sorted(frontal_base - frontal)}")

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
