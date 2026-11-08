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
    zona: 'Zona horaria',
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
    zona: 'Zona horaria',
    ningunaVisible: 'No hay ninguna empresa que puedas ver.',
  },

  zonas: {
    nombres: {
      peninsula: 'Península y Baleares',
      canarias: 'Canarias',
      ceuta: 'Ceuta y Melilla',
    },
    deLaEmpresa: 'de la empresa',
    propia: 'propia',
    cambiar: 'Cambiar',
    cambiarLaDe: 'Cambiar la zona de {{nombre}}',
    titulo: 'Zona horaria de {{nombre}}',
    cargando: 'la zona',
    leyenda: 'Zona horaria',
    laDeLaEmpresa: 'La de la empresa',
    avisoLoFechado:
      'Lo ya fechado no cambia: cada documento y cada movimiento conserva el día con el que se ' +
      'guardó.',
    avisoLaHora:
      'Durante la hora que sigue al cambio, como mucho, una operación fechada hoy puede quedar ' +
      'antes que la última de su almacén. Entonces no se admite hasta que pasa esa hora.',
    avisoLosAlmacenes:
      'También cambia el día de los almacenes de esta empresa que no tienen zona propia.',
    guardar: 'Guardar la zona',
    guardando: 'Guardando…',
    cancelar: 'Cancelar',
    recargar: 'Cargar la versión actual',
    cambiada: 'La zona de {{nombre}} es ahora {{zona}}.',
    heredada: '{{nombre}} toma ahora la zona de su empresa: {{zona}}.',
  },
};

export type TextosDeOrganizacion = typeof organizacion;
