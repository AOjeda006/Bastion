import { useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { clavesDeMaestros } from '../../maestros/api/claves.ts';
import { buscarArticulos, type ArticuloEncontrado } from '../../maestros/api/maestros.ts';
import { VIDA_DE_UN_MAESTRO } from '../../maestros/ui/nombres.ts';

/** Lo que se espera desde la última tecla antes de preguntar: una petición por palabra, no por letra. */
const ESPERA_AL_ESCRIBIR = 250;

/**
 * La celda del artículo: se escribe, y se elige de lo que casa (ADR-0065 §8).
 *
 * Es un *combobox* de ARIA con su lista: las flechas recorren las opciones, Intro elige la marcada
 * —o la única, si solo hay una—, y Escape cierra la lista. **La búsqueda es la del listado del
 * catálogo** (`?q=`), que mira el código y la descripción, así que lo que se ofrece es lo mismo que
 * encontraría la pantalla de artículos.
 *
 * Escribir otra vez **deja la línea sin artículo** hasta que se elige uno: lo escrito es una
 * búsqueda, no un artículo, y mandarlo así daría el artículo de antes con el texto de otro. Con la
 * lista cerrada, Intro es de la rejilla, que decide si añade una línea.
 */
export function BuscadorDeArticulo({
  id,
  etiqueta,
  articuloId,
  error,
  idDelError,
  referencia,
  alElegir,
  alSalir,
  alPulsarIntro,
}: {
  id: string;
  etiqueta: string;
  /** El artículo elegido, o vacío: el valor del campo del formulario. */
  articuloId: string;
  error: string | null;
  idDelError: string;
  /** Para que el formulario pueda llevar el foco aquí, con el rechazo que sea de este campo. */
  referencia: React.Ref<HTMLInputElement>;
  alElegir: (articulo: ArticuloEncontrado | null) => void;
  alSalir: () => void;
  alPulsarIntro: (evento: React.KeyboardEvent<HTMLInputElement>) => void;
}): React.JSX.Element {
  const { t } = useTranslation();
  const [texto, setTexto] = useState('');
  const [busqueda, setBusqueda] = useState('');
  const [abierta, setAbierta] = useState(false);
  const [marcada, setMarcada] = useState(-1);
  const lista = `${id}-opciones`;

  useEffect(() => {
    const espera = setTimeout(() => {
      setBusqueda(texto.trim());
    }, ESPERA_AL_ESCRIBIR);

    return () => {
      clearTimeout(espera);
    };
  }, [texto]);

  const consulta = useQuery({
    queryKey: clavesDeMaestros.articulos(busqueda),
    queryFn: () => buscarArticulos(busqueda),
    staleTime: VIDA_DE_UN_MAESTRO,
    enabled: abierta && busqueda !== '',
  });

  const encontrados = abierta && busqueda !== '' ? (consulta.data ?? []) : [];
  const desplegada = encontrados.length > 0;

  const elegir = (articulo: ArticuloEncontrado): void => {
    setTexto(
      articulo.nombre === null ? articulo.codigo : `${articulo.codigo} · ${articulo.nombre}`,
    );
    setAbierta(false);
    setMarcada(-1);
    alElegir(articulo);
  };

  const alPulsar = (evento: React.KeyboardEvent<HTMLInputElement>): void => {
    switch (evento.key) {
      case 'ArrowDown':
        evento.preventDefault();
        setAbierta(true);
        setMarcada((actual) => Math.min(actual + 1, encontrados.length - 1));
        return;
      case 'ArrowUp':
        evento.preventDefault();
        setMarcada((actual) => Math.max(actual - 1, 0));
        return;
      case 'Escape':
        if (abierta) {
          evento.preventDefault();
          setAbierta(false);
          setMarcada(-1);
        }
        return;
      case 'Enter': {
        const elegido = encontrados[marcada] ?? (encontrados.length === 1 ? encontrados[0] : null);

        if (elegido !== undefined && elegido !== null) {
          evento.preventDefault();
          elegir(elegido);
        } else {
          alPulsarIntro(evento);
        }
        return;
      }
    }
  };

  const descripciones = [error !== null && idDelError, `${id}-estado`].filter(Boolean).join(' ');

  return (
    <div className="relative">
      <input
        id={id}
        ref={referencia}
        type="text"
        role="combobox"
        aria-label={etiqueta}
        aria-autocomplete="list"
        aria-expanded={desplegada}
        aria-controls={lista}
        aria-activedescendant={
          desplegada && marcada >= 0 ? `${lista}-${String(marcada)}` : undefined
        }
        aria-invalid={error !== null}
        aria-describedby={descripciones}
        autoComplete="off"
        value={texto}
        onChange={(evento) => {
          setTexto(evento.target.value);
          setAbierta(true);
          setMarcada(-1);

          if (articuloId !== '') {
            alElegir(null);
          }
        }}
        onKeyDown={alPulsar}
        onBlur={() => {
          setAbierta(false);
          setMarcada(-1);
          alSalir();
        }}
        className="w-56 rounded border border-neutral-300 px-2 py-1"
      />
      <div
        id={lista}
        role="listbox"
        aria-label={etiqueta}
        hidden={!desplegada}
        className="absolute z-10 mt-1 max-h-60 w-72 overflow-y-auto rounded border border-neutral-300 bg-white shadow"
      >
        {encontrados.map((articulo, posicion) => (
          <div
            key={articulo.id}
            id={`${lista}-${String(posicion)}`}
            role="option"
            aria-selected={posicion === marcada}
            tabIndex={-1}
            onMouseDown={(evento) => {
              // Antes de que el campo pierda el foco: con el `blur` primero, la lista se cerraría y
              // el clic no llegaría a ninguna opción.
              evento.preventDefault();
              elegir(articulo);
            }}
            className="cursor-pointer px-2 py-1 aria-selected:bg-neutral-100"
          >
            {articulo.nombre === null ? articulo.codigo : `${articulo.codigo} · ${articulo.nombre}`}
          </div>
        ))}
      </div>
      <p id={`${id}-estado`} role="status" className="text-xs text-neutral-600">
        {abierta && busqueda !== '' && consulta.isFetching && t('inventario.ajustes.alta.buscando')}
        {abierta &&
          busqueda !== '' &&
          consulta.isSuccess &&
          !consulta.isFetching &&
          encontrados.length === 0 &&
          t('inventario.ajustes.alta.ningunArticulo')}
      </p>
    </div>
  );
}
