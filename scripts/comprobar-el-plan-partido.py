"""Que partir el PLAN no perdió nada: cada línea del PLAN de antes está en uno de los ficheros de después.

Uso:  python scripts/comprobar-el-plan-partido.py
      python scripts/comprobar-el-plan-partido.py <antes> <después>
      python scripts/comprobar-el-plan-partido.py <antes> --disco

Sin argumentos compara el commit que creó `docs/historia/` con su padre: es la comprobación del
ítem 3.1, que partió el PLAN, y se puede repetir siempre. `--disco` lee el después del árbol de
trabajo, para comprobar antes de commitear. Lee cada ref con `git show`, no del disco.

Qué compara, dicho una vez:

- **antes**: `docs/PLAN.md`. **Después**: `docs/PLAN.md` y cada `docs/historia/*.md`.
- Las líneas se cuentan como un multiconjunto: cada línea de antes tiene que estar en el después
  tantas veces como estaba, o más. Una línea que se repite —una vacía, un `|---|---|`— cuenta
  cada vez que aparece.
- Lo único que se deshace antes de comparar es lo que el movimiento cambia a propósito: en la
  historia, `](../adr/` vuelve a ser `](adr/`, porque el fichero está una carpeta más abajo.

Sale con 1 si falta alguna línea, y también si el después no tiene ningún fichero de historia o si
de la historia no sale ninguna línea del antes: un comparador que no ve historia no compara nada.
Imprime el tamaño de cada lado, cuántas líneas del antes salen de la historia y cuántas añade el
después, que son las cabeceras y el índice.
"""
import collections
import os
import subprocess
import sys

PLAN = "docs/PLAN.md"
HISTORIA = "docs/historia"


def git(*argumentos):
    return subprocess.run(
        ["git", *argumentos], capture_output=True, text=True, encoding="utf-8", check=True
    ).stdout


def del_commit_que_partio():
    commits = git("log", "--diff-filter=A", "--format=%H", "--", f"{HISTORIA}/fase-0.md").split()
    if not commits:
        sys.exit(f"ningún commit añade {HISTORIA}/fase-0.md: no hay partición que comprobar")
    despues = commits[-1]
    return f"{despues}~1", despues


def ficheros(ref):
    if ref is None:
        historia = sorted(
            f"{HISTORIA}/{nombre}" for nombre in os.listdir(HISTORIA) if nombre.endswith(".md")
        ) if os.path.isdir(HISTORIA) else []
        leer = lambda ruta: open(ruta, encoding="utf-8", newline="").read()
    else:
        historia = [
            ruta
            for ruta in git("ls-tree", "-r", "--name-only", ref, "--", HISTORIA).splitlines()
            if ruta.endswith(".md")
        ]
        leer = lambda ruta: git("show", f"{ref}:{ruta}")
    return {ruta: leer(ruta) for ruta in [PLAN, *historia]}


def lineas(texto):
    if "\r" in texto:
        sys.exit("hay un CR: los ficheros del PLAN van con LF")
    return texto[:-1].split("\n") if texto.endswith("\n") else texto.split("\n")


def main(argumentos):
    if not argumentos:
        antes, despues = del_commit_que_partio()
    elif len(argumentos) == 2:
        antes, despues = argumentos[0], (None if argumentos[1] == "--disco" else argumentos[1])
    else:
        sys.exit((__doc__ or "").split("\n\n")[1])

    viejo = lineas(git("show", f"{antes}:{PLAN}"))
    nuevos = ficheros(despues)
    historia = [ruta for ruta in nuevos if ruta != PLAN]

    contadas = collections.Counter()
    de_la_historia = collections.Counter()
    for ruta, texto in nuevos.items():
        for linea in lineas(texto):
            if ruta != PLAN:
                linea = linea.replace("](../adr/", "](adr/")
                de_la_historia[linea] += 1
            contadas[linea] += 1

    pedidas = collections.Counter(viejo)
    faltan = {linea: n - contadas[linea] for linea, n in pedidas.items() if contadas[linea] < n}
    salen_de_la_historia = sum(min(n, de_la_historia[linea]) for linea, n in pedidas.items())
    anadidas = sum((contadas - pedidas).values())

    print(f"antes  {antes}: {PLAN}, {len(viejo)} líneas, {len(git('show', f'{antes}:{PLAN}').encode())} bytes")
    for ruta, texto in nuevos.items():
        print(f"después {despues or 'disco'}: {ruta}, {len(lineas(texto))} líneas, {len(texto.encode())} bytes")
    print(f"líneas del antes que salen de la historia: {salen_de_la_historia}")
    print(f"líneas que el después añade: {anadidas}")

    if not historia:
        print(f"ROJO: el después no tiene ningún fichero en {HISTORIA}/")
        return 1
    if salen_de_la_historia == 0:
        print("ROJO: de la historia no sale ninguna línea del antes")
        return 1
    if faltan:
        print(f"ROJO: faltan {sum(faltan.values())} líneas del antes, {len(faltan)} distintas:")
        numeradas = {}
        for numero, linea in enumerate(viejo, start=1):
            numeradas.setdefault(linea, numero)
        for linea, n in sorted(faltan.items(), key=lambda par: numeradas[par[0]])[:20]:
            print(f"  línea {numeradas[linea]} (falta {n} vez/veces): {linea[:100]}")
        return 1
    print("VERDE: cada línea del antes está en el después")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
