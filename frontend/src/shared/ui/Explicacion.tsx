/**
 * Por qué una marca dice lo que dice, escrito debajo de la tabla o de la ficha.
 *
 * **La explicación de una marca no va en un `title`.** Un `title` no llega al teclado ni al tacto, y
 * tampoco de forma fiable al lector de pantalla. Va en un párrafo visible, uno por clase de valor y
 * no uno por fila, y solo si alguna fila lleva esa marca. Cada sitio que la lleva lo señala con
 * `aria-describedby`: quien mira lo tiene escrito, y el lector de pantalla lo lee con la celda.
 *
 * Solo el detalle lleva el `id`, porque es la descripción de la celda, y el nombre de la celda ya
 * es la marca.
 */
export function ExplicacionDeMarca({
  id,
  marca,
  detalle,
}: {
  id: string;
  marca: React.ReactNode;
  detalle: string;
}): React.JSX.Element {
  return (
    <p className="mt-3 text-xs text-neutral-700">
      {marca} <span id={id}>{detalle}</span>
    </p>
  );
}
