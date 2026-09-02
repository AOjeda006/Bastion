# `empresas` — las empresas del grupo

**Propósito.** Enseñar las empresas dadas de alta. No es el selector: el selector vive en el
armazón (`app/SelectorDeEmpresa.tsx`) porque decide con cuál se opera, y eso afecta a todas las
pantallas.

## Rutas

| Ruta        | Exigencia                          | Título   |
| ----------- | ---------------------------------- | -------- |
| `/empresas` | permiso `organizacion.empresa.ver` | Empresas |

Parámetros de URL: `?pagina=` y `?tamanio=`.

## Claves de consulta

`api/claves.ts`, jerárquicas:

```
['empresas']
['empresas', 'lista']
['empresas', 'lista', { pagina, tamanio }]
```

## Lo que no es evidente

- El listado devuelve **las empresas que el usuario alcanza**, que no tienen por qué ser todas: lo
  decide el servidor. La interfaz no filtra nada por su cuenta.
- Una empresa bloqueada (R16) contesta 404 a su propio `GET`, así que **no aparece** aquí ni hay
  forma de desbloquearla desde el frontal. Está anotado en `docs/PLAN.md`; ensanchar el ámbito de
  lectura desde el navegador sería exactamente lo contrario de lo que el filtro protege.
