import type { ListadoDeTarifas } from '../model/listado.ts';

/**
 * Las claves de consulta de las tarifas, todas en un módulo y jerárquicas. Mismo criterio que las
 * de artículos y categorías: por prefijo se invalida todo —`['tarifas']`—, y una clave escrita a
 * mano en un componente es una invalidación que no invalida, porque se parece a la buena y no es la
 * misma.
 *
 * La empresa NO forma parte de la clave: va dentro del testigo y quien filtra es el servidor (R8).
 */
export const clavesDeTarifas = {
  todo: ['tarifas'] as const,
  listas: () => [...clavesDeTarifas.todo, 'lista'] as const,
  lista: (listado: ListadoDeTarifas) => [...clavesDeTarifas.listas(), listado] as const,
};
