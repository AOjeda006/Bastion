import type { ListadoDeTerceros } from '../model/listado.ts';

/**
 * Las claves de consulta de esta funcionalidad, todas en un módulo y jerárquicas.
 *
 * Jerárquicas para poder invalidar por prefijo —`['terceros']` alcanza a todas las listas— y en un
 * módulo porque una clave escrita a mano en un componente es una invalidación que no invalida: se
 * parece a la buena y no es la misma.
 *
 * La empresa NO forma parte de la clave, igual que en los demás listados: va dentro del testigo y
 * quien filtra es el servidor (R8). Por eso el cambio de empresa no invalida esto a mano — vacía
 * la caché entera.
 */
export const clavesDeTerceros = {
  todo: ['terceros'] as const,
  listas: () => [...clavesDeTerceros.todo, 'lista'] as const,
  lista: (listado: ListadoDeTerceros) => [...clavesDeTerceros.listas(), listado] as const,
};
