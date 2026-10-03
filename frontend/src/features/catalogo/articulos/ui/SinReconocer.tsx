/**
 * Lo que esta versión de la pantalla no sabe interpretar: la marca en su sitio, y la explicación
 * escrita aparte.
 *
 * Los enumerados viajan como texto, y el frontal se despliega aparte del backend: un valor nuevo
 * llega antes de que esta pantalla lo conozca. Lo que no se puede interpretar se dice. La celda en
 * blanco es lo que se ve cuando algo se rompe, y el texto crudo que llegó parecería un valor bueno.
 *
 * **La explicación no va en un `title`.** Un `title` no llega al teclado ni al tacto, y tampoco de
 * forma fiable al lector de pantalla. Va en un párrafo visible, uno por clase de valor y no uno por
 * fila, y cada sitio que lleva la marca lo señala con `aria-describedby`. Quien mira lo tiene
 * escrito, y el lector de pantalla lo lee con la celda.
 */
export function SinReconocer({ texto }: { texto: string }): React.JSX.Element {
  return (
    <span className="rounded border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-xs text-amber-900">
      {texto}
    </span>
  );
}

/**
 * Por qué sale la marca. Solo el detalle lleva el `id`, porque es la descripción de la celda, y el
 * nombre de la celda ya es la marca.
 */
export function ExplicacionDeSinReconocer({
  id,
  marca,
  detalle,
}: {
  id: string;
  marca: string;
  detalle: string;
}): React.JSX.Element {
  return (
    <p className="mt-3 text-xs text-neutral-700">
      <SinReconocer texto={marca} /> <span id={id}>{detalle}</span>
    </p>
  );
}
