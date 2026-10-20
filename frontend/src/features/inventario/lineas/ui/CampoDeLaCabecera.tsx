/** Lo que un campo de la cabecera le pasa a su control: el nombre y a qué se refiere su error. */
export interface PropiedadesDelCampo {
  readonly id: string;
  readonly 'aria-invalid': boolean;
  readonly 'aria-describedby': string | undefined;
}

/** Un campo de la cabecera del alta, con su etiqueta encima y su error debajo. */
export function CampoDeLaCabecera({
  campo,
  etiqueta,
  error,
  children,
}: {
  campo: string;
  etiqueta: string;
  error: string | null;
  children: (propiedades: PropiedadesDelCampo) => React.ReactNode;
}): React.JSX.Element {
  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={campo} className="font-medium">
        {etiqueta}
      </label>
      {children({
        id: campo,
        'aria-invalid': error !== null,
        'aria-describedby': error === null ? undefined : `${campo}-error`,
      })}
      {error !== null && (
        <p id={`${campo}-error`} role="alert" className="text-red-800">
          {error}
        </p>
      )}
    </div>
  );
}
