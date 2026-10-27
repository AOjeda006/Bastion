/**
 * Los textos de Organización en castellano, y **la forma** que el inglés tiene que cumplir.
 *
 * **Viajan con las rutas de Organización, y no en el diccionario del arranque** (ADR-0071).
 * Hasta el 3.1 eran el espacio de nombres `organizacion` de `es.ts`. Ahora los trae el enrutador
 * junto con la pantalla, en el idioma activo, y entran en el mismo espacio de nombres:
 * `t('organizacion.…')` sigue siendo la misma clave. Por eso solo los lee el código de su
 * funcionalidad: el armazón, o una pantalla de otro módulo, los encontraría sin cargar, y
 * `LosTextosViajanConSusRutas` lo comprueba.
 *
 * Se importan por su TIPO desde donde haga falta, y por su VALOR solo desde `diccionarios.ts` y
 * los tests, como el diccionario: lo vigila el *linter*.
 */
export const organizacion = {
  almacenes: {
    cargando: 'los almacenes',
    tabla: 'Almacenes de la empresa activa',
    codigo: 'Código',
    nombre: 'Nombre',
    tipo: 'Tipo',
    poblacion: 'Población',
    paginaVacia: 'Esta página no tiene almacenes. Vuelve a la anterior.',
    ningunoTodavia: 'Todavía no hay ningún almacén dado de alta en esta empresa.',
  },

  empresas: {
    cargando: 'las empresas',
    tabla: 'Empresas dadas de alta',
    nif: 'NIF',
    razonSocial: 'Razón social',
    poblacion: 'Población',
    divisa: 'Divisa',
    ningunaVisible: 'No hay ninguna empresa que puedas ver.',
  },
};

export type TextosDeOrganizacion = typeof organizacion;
