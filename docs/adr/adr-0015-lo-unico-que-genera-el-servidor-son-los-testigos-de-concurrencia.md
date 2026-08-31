---
tipo: referencia
stack: [dotnet, efcore, postgresql]
aplica_a: [persistencia, auditoria, testing]
tags: [adr, r11, r13, auditoria, concurrencia, xmin, ef-core]
revisado: 2026-08-31
---

# ADR-0015: Lo único que genera el servidor son los testigos de concurrencia

- **Estado:** aceptado
- **Fecha:** 2026-08-31
- **Sustituye a:** el **punto 2** del
  [ADR-0012](adr-0012-la-traza-va-en-la-misma-transaccion-que-el-cambio.md) («Una sola fase, porque
  las claves se conocen antes de guardar»). El resto del ADR-0012 sigue vigente tal cual.

## Contexto

El interceptor de auditoría del 0.7 recoge la traza en **una sola fase**, dentro de
`SavingChanges`. La receta canónica son dos —recoger antes, completar después—, y existe porque en
el caso general la clave de un `INSERT` la pone la base y no se sabe hasta que ha ido. El ADR-0012
justificó la fase única con una premisa comprobada por `LasClavesSeConocenAntesDeGuardarTests`:

> **Ninguna propiedad** —sea o no clave— viene del servidor, en las cinco formas que tiene de venir.

Ese mismo ADR escribió cuándo dejaría de ser cierta, y con qué había que hacer entonces:

> El día que alguien añada una columna con `DEFAULT now()`, una clave `IDENTITY` o el testigo de
> concurrencia del 0.9, ese test se pone rojo **antes** de que la traza empiece a escribir claves
> vacías […]. Y lo que hay que hacer entonces no es añadir la propiedad a una lista de excepciones:
> es reabrir esta decisión.

El 0.9 declara `xmin` como testigo de concurrencia en seis entidades. El día previsto ha llegado y
el test se puso rojo, como estaba escrito que haría. Esto es el cumplimiento de esa cláusula.

## Decisión

**La premisa se parte en dos afirmaciones, y las dos se comprueban por separado.**

1. **Ninguna propiedad AUDITADA la pone la base de datos.**
   `Ninguna_propiedad_auditada_la_pone_la_base_de_datos`.
2. **Lo único que genera el servidor son los testigos de concurrencia, enumerados por nombre.**
   `Lo_unico_que_genera_el_servidor_son_los_testigos_de_concurrencia` compara las **dos listas
   enteras** —la del modelo y la escrita a mano, `Almacen.Version`, `Ejercicio.Version`,
   `Empresa.Version`, `Rol.Version`, `Serie.Version`, `Usuario.Version`— en el mismo orden.

Y una tercera que no estaba y hace falta al partirlo así:

3. **Lo que genera el servidor es de verdad un testigo**, no algo que se le parezca.
   `Todo_lo_que_genera_el_servidor_es_de_verdad_un_testigo_de_concurrencia` comprueba, propiedad a
   propiedad, que es `uint`, que se regenera en cada escritura y que está marcada como testigo.

### Por qué esto no es debilitar la premisa

Lo fácil habría sido añadir `Version` a una lista de excepciones del test de antes. Se descarta, y
no por gusto: una lista de excepciones dice **«esta propiedad no cuenta»** sin decir por qué, y la
siguiente que se añada —un `DEFAULT now()` en una columna auditada— entra por la misma puerta con la
misma facilidad y sin que nadie tenga que argumentar nada.

Partirlo en dos afirmaciones dice otra cosa: **cuál es la propiedad que de verdad sostiene la fase
única** —la 1, que habla solo de lo auditado— y **qué es lo único que se ha autorizado a salirse**
—la 2, que es una lista cerrada y comparada entera—.

Las tres consecuencias de la forma elegida:

- Una columna nueva con `DEFAULT` en una entidad auditada pone roja la **1**, que es la que importa,
  y el mensaje manda reabrir este ADR.
- Un testigo que **desaparece** del modelo pone roja la **2**, porque las listas se comparan enteras
  y no «lo que sobra». Un recurso que se queda sin control de concurrencia es tan grave como uno que
  gana una columna generada, y antes no se veía.
- Una propiedad llamada `Version` con un `DEFAULT` en la base pasaría por testigo en la **2**, que
  compara por nombre. La **3** la caza por lo que la hace ser un testigo.

### Por qué el testigo no rompe la fase única, ahora que se dice explícitamente

Porque **el testigo no va a la traza**. La traza escribe los valores de las propiedades auditadas, y
`xmin` no es una de ellas: es una columna de sistema de PostgreSQL que ni el dominio conoce ni nadie
audita. El interceptor sigue sin necesitar ningún valor que la base ponga después del `INSERT`.

Y la clave primaria sigue completa antes de guardar, que es la otra mitad de lo que la fase única
necesitaba: `Toda_entidad_tiene_su_clave_completa_antes_de_guardar` no ha cambiado.

## Consecuencias

- El ADR-0012 se lee entero **menos su punto 2**, que remite aquí. Una decisión aceptada no se
  edita: se sustituye, y queda el rastro de por qué.
- La lista de seis testigos es de mantenimiento manual **a propósito**. Cuando la fase 1 añada
  entidades con `xmin`, ese rojo obliga a escribir sus nombres aquí, que es la única forma de que
  «cuáles son» siga siendo una decisión y no un efecto secundario.
- Si algún día hace falta de verdad una propiedad generada por el servidor **y auditada**, la fase
  única del interceptor deja de valer y hay que volver a la receta de dos fases. Ese día se abre otro
  ADR; no se toca la lista.
