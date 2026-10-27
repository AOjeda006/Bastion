---
tipo: referencia
stack: [dotnet, aspnetcore, efcore]
aplica_a: [api-rest, inventario, catalogo, organizacion]
tags: [adr, listado, paginacion, filtro, q, contrato, transferencia, adr-0068]
revisado: 2026-10-27
---

# ADR-0069: Un `?q=` sin filtro declarado llega y no filtra nada

- **Estado:** aceptado. **Enmendado por el ADR-0072 (puntos 1 y 3 de la decisión).**
  - El [ADR-0072](adr-0072-un-listado-enlaza-tres-piezas-y-el-texto-solo-quien-lo-mira.md)
    enmienda, el 2026-10-27, los puntos 1 y 3: un `?q=` con algo escrito a uno de esos listados
    es un `400` `filtro-no-admitido`, ya no lo publican, y el rechazo que el punto 3 dejaba
    propuesto se hace en el ítem 3.2.
- **Fecha:** 2026-10-22
- **Sale del ítem 2.19**, de un hallazgo al escribir el borde de la transferencia, en `79fd5d6`.
  Su mensaje dice que la enmienda iba al PLAN, y va aquí, porque lo que corrige es un ADR.
- **Enmienda el [ADR-0068](adr-0068-la-transferencia-se-abre-como-el-ajuste-con-dos-puntas-en-cada-linea.md)
  §1**, en el punto «Sin `?q=`»: el borde no contesta que el recurso no admite filtro de texto.

## Contexto

El §1 del ADR-0068 decía que el listado de las transferencias no lleva `?q=`, y que, «como en las
líneas de tarifa, el borde contesta que este recurso no admite filtro de texto». **La primera
mitad es cierta y la segunda no.** El borde no contesta nada: `Paginador.Filtrar` devuelve la
consulta tal cual cuando el repositorio no declara `Filtro` en sus `CriteriosDe`, y la respuesta
es la misma página que sin el parámetro:

```csharp
if (criterios.Filtro is null || string.IsNullOrWhiteSpace(filtro))
{
    return consulta;
}
```

Lo dice también el contrato. **Los 23 listados paginados publican `q`**, lo declaren o no, porque
el parámetro viene de `ConsultaPaginada`. Lo cuenta esta orden, sobre `docs/api/openapi.json`, que
imprime cada ruta con su `q`:

```bash
python -X utf8 - <<'EOF'
import json
d = json.load(open("docs/api/openapi.json", encoding="utf-8"))
for ruta, ops in d["paths"].items():
    ps = [p.get("name") for p in ops.get("get", {}).get("parameters", [])]
    if "page" in ps:
        print("q" if "q" in ps else "-", ruta)
EOF
```

Cinco de esos repositorios no declaran filtro: las líneas de tarifa, las transferencias, los
ejercicios, los tipos de cambio y las conversiones de unidades. Y el saldo, que no pasa por el
paginador, tampoco busca nada. **El comentario de las líneas de tarifa decía lo mismo que el
ADR-0068**, y el §1 lo copió de ahí: la frase venía de antes y nadie la había medido.

El caso de la lista de la transferencia, `La_lista_va_de_la_ultima_fecha_de_envio_…_descartada`
de `LaSuperficieDeLaTransferenciaTests`, lo afirma desde `79fd5d6`: `?q=no-esta-en-ninguna` trae
las tres transferencias de la escena.

## Decisión

1. > **Enmendado por el ADR-0072 (2026-10-27).** Ya no llega: los seis listados sin filtro
   > declarado dejan de publicar `q`, y un `?q=` con algo escrito es un `400`
   > `filtro-no-admitido`. En blanco, o sin él, pasa.

   **El `?q=` de un listado sin filtro declarado llega y no filtra nada**, en la transferencia y en
   los otros cuatro. Es lo que hace el código, y lo que dice ya la cabecera de
   `ConsultaDeTransferencias` y la ayuda de `TransferenciasController`.
2. **El comentario de `RepositorioDeLineasDeTarifa` se reescribe** con lo que hace, en este mismo
   commit. Es la regla de `AGENTS.md`: un comentario que contradice la medida se reescribe.
3. > **Enmendado por el ADR-0072 (2026-10-27).** Se hace en el ítem 3.2, que es la exportación:
   > un fichero con todas las filas saldría con el nombre de lo que se creía haber buscado. El
   > contrato cambia en los seis listados, y el paginador común lanza si le llega un texto que
   > el repositorio no mira.

   **Rechazarlo con un `400` no se hace aquí, y queda propuesto.** Sería un cambio del contrato en
   cinco listados, que dejarían de publicar `q`, y un cambio del paginador común. No lo pide ningún
   ítem, y cambiar el contrato por un hallazgo no es su sitio. La propuesta, con su disparador, va a
   *Notas / riesgos* del PLAN, y el cierre de la fase 2 decide si pasa al traspaso.

## Consecuencias

- **El ADR-0068 §1 dice ahora la verdad sobre el borde**, a través de su nota: no hay filtro de
  texto, y un `?q=` no se rechaza. El ADR-0068 lo anota en su *Estado* y al principio del punto.
- **Un cliente que mande `q` a uno de esos cinco listados recibe la página entera.** El frontal no
  lo hace: sus pantallas de esos recursos no tienen buscador.
- **Si algún día se rechaza**, el caso de la lista de la transferencia es el que se pone rojo, y
  es el que hay que cambiar, junto con el contrato versionado.
