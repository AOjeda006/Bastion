/** La celda de una línea, con el error de su campo debajo. */
export function Celda({
  id,
  error,
  children,
}: {
  /** El del control de dentro: el error se llama como él, con `-error`. */
  id: string;
  error: string | null;
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <td className="py-2 pr-2">
      {children}
      {error !== null && (
        <p id={`${id}-error`} role="alert" className="mt-1 max-w-56 text-xs text-red-800">
          {error}
        </p>
      )}
    </td>
  );
}
