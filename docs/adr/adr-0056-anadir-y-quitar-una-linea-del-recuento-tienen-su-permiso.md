---
tipo: referencia
stack: [dotnet]
aplica_a: [autorizacion, inventario, api-rest]
tags: [adr, recuento, permisos, autorizacion, adr-0055]
revisado: 2026-10-07
---

# ADR-0056: Añadir y quitar una línea del recuento tienen su permiso, aparte del de contar

- **Estado:** aceptado
- **Fecha:** 2026-10-07
- **Sale del 2.12**, del paso que pone las escrituras en las líneas del recuento.
- **Enmienda el ADR-0055 §13**, en los nombres de los permisos: a los seis que lista se suman dos.

## Contexto

El ADR-0055 §13 nombra los permisos del recuento: `inventario.recuento.ver`, `…abrir`, `…contar`,
`…confirmar`, `…anular` y `…descartar`. No dice nada de añadir ni de quitar una línea, que el §5 y
el §6 permiten. El primer código de esas dos acciones las puso bajo `…contar`, con un argumento:
son la misma tarea vista desde el estante.

El carril funcional lo paró. `CadaAccionDeclaraSuPermisoTests.Escribir_y_modificar_no_comparten_permiso_aunque_los_escriba_el_mismo_codigo`
dio este rojo:

> «inventario.recuento.contar» abre RecuentosController.Contar y RecuentosController.AnadirLinea y
> RecuentosController.QuitarLinea

La regla existe desde la fase 0, y su motivo está escrito en el test: hay perfiles que dan de alta y
no corrigen, y al revés, y un permiso compartido no permite expresarlo. El encargo del 2026-10-05
pide lo mismo para el recuento: «un permiso por acción».

## Decisión

1. **Contar, añadir y quitar son tres permisos.**
   - **Contar** dice cuánto hay de una clave que ya está en el recuento.
   - **Añadir** decide qué claves entran. Una clave nueva lleva su coste al ajuste (ADR-0055 §6), y
     quien cuenta lo que tiene delante no tiene por qué poder meter en el recuento lo que el sistema
     no conocía.
   - **Quitar** decide qué claves salen, y su clave queda como está (ADR-0055 §5).

   Un administrador que solo quiera dar el conteo lo puede hacer.
2. **Los nombres van bajo `inventario.recuento.*`**: `inventario.recuento.agregar-linea` e
   `inventario.recuento.quitar-linea`. Así todo lo del recuento sale junto en la lista de permisos,
   como lo dice el §13. Ya hay precedente de esa forma, con el objeto pegado al verbo:
   `identidad.pertenencia.asignar-rol` y `identidad.pertenencia.retirar-rol`. El verbo es el de la
   casa para los hijos, `agregar` y `quitar`, y no `anadir`, aunque así se llamen la acción y el caso
   de uso.
3. **`inventario.recuento.contar` no cambia.** Es el que el ADR-0055 ya nombró.

## Consecuencias

- **El catálogo de Inventario pasa de siete permisos a diez** con este paso. Los de confirmar,
  anular y descartar entran con sus acciones.
- **El ADR-0055 §13 lleva la nota de esta enmienda.**
- **La pantalla** enseña cada acción según el permiso que la abre, no según el de contar.

## Procedencia

La enmienda la decidió el agente al ver el rojo del carril funcional, con la regla de la fase 0 y el
encargo del 2026-10-05, que pide «un permiso por acción».
