import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { anularRecuento, confirmarRecuento, descartarRecuento } from '../api/recuentos.ts';
import { esquemaDeMotivo, type DatosDeMotivo } from '../model/formularios.ts';
import type { VistaDeLineas } from '../model/listado.ts';
import { RECHAZO_DEL_MOTIVO } from '../model/rechazos.ts';
import type { FichaDeRecuento, Recuento } from '../model/recuento.ts';
import { tipoDeFallo } from '@/shared/api/errores.ts';
import { intentoPara, type Intento } from '@/shared/api/intento.ts';
import { PERMISOS } from '@/shared/sesion/permisos.ts';
import { concede } from '@/shared/sesion/sesion.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';

/**
 * Los rechazos de la confirmación que llevan a las líneas que lo explican. En un `Map` y no en un
 * objeto: el `type` viene de la red, y un objeto tiene `constructor` por herencia.
 */
const VISTA_DEL_RECHAZO: ReadonlyMap<string, VistaDeLineas> = new Map([
  ['recuento-teorico-cambiado', 'teorico-cambiado'],
  ['recuento-con-lineas-sin-contar', 'sin-contar'],
]);

/**
 * Confirmar, anular y descartar: lo que cierra un recuento, cada uno con su permiso.
 *
 * <b>Los tres citan la ficha que se ve</b>: su versión en el `If-Match` y, al confirmar, la huella
 * del teórico (ADR-0057). Mientras la ficha se está leyendo otra vez, los botones esperan: con la
 * versión de antes, el servidor contestaría `412` a algo que nadie ha provocado.
 *
 * <b>Un rechazo de la confirmación lleva a lo que lo explica.</b> Si el teórico ha cambiado desde
 * que se leyó, la pantalla enseña las líneas con el teórico cambiado; si quedan líneas sin contar,
 * esas. La ficha se lee otra vez, y volver a confirmar manda la huella nueva con una clave nueva.
 */
export function AccionesDelRecuento({
  ficha,
  ocupada,
  alHacer,
  alFallar,
  alIrALaVista,
}: {
  ficha: FichaDeRecuento;
  /** Si la ficha se está leyendo otra vez: hasta que llega, no se ofrece nada. */
  ocupada: boolean;
  alHacer: (clase: 'confirmado' | 'anulado' | 'descartado', recuento: Recuento) => Promise<void>;
  alFallar: (error: unknown) => Promise<void>;
  alIrALaVista: (vista: VistaDeLineas) => void;
}): React.JSX.Element | null {
  const { t } = useTranslation();
  const sesion = useSesionAbierta();
  const { recuento, version } = ficha;

  const puedeConfirmar =
    recuento.estado === 'enCurso' && concede(sesion, PERMISOS.recuentoConfirmar);
  const puedeDescartar =
    recuento.estado === 'enCurso' && concede(sesion, PERMISOS.recuentoDescartar);
  const puedeAnular = recuento.estado === 'confirmado' && concede(sesion, PERMISOS.recuentoAnular);

  if (!puedeConfirmar && !puedeDescartar && !puedeAnular) {
    return null;
  }

  return (
    <div className="mt-8 flex flex-col items-start gap-4">
      {puedeConfirmar && (
        <Confirmacion
          recuento={recuento}
          version={version}
          ocupada={ocupada}
          alHacer={alHacer}
          alFallar={async (error) => {
            const vista = VISTA_DEL_RECHAZO.get(tipoDeFallo(error) ?? '');

            if (vista !== undefined) {
              alIrALaVista(vista);
            }

            await alFallar(error);
          }}
        />
      )}

      {puedeAnular && (
        <CierreConMotivo
          id="anulacion"
          textos={{
            abrir: t('inventario.recuentos.ficha.anular'),
            pregunta: t('inventario.recuentos.ficha.preguntaAnular'),
            si: t('inventario.recuentos.ficha.siAnular'),
            enviando: t('inventario.recuentos.ficha.anulando'),
          }}
          ocupada={ocupada}
          enviar={(motivo, clave) => anularRecuento({ id: recuento.id, version, clave, motivo })}
          alHacer={(hecho) => alHacer('anulado', hecho)}
          alFallar={alFallar}
        />
      )}

      {puedeDescartar && (
        <CierreConMotivo
          id="descarte"
          textos={{
            abrir: t('inventario.recuentos.ficha.descartar'),
            pregunta: t('inventario.recuentos.ficha.preguntaDescartar'),
            si: t('inventario.recuentos.ficha.siDescartar'),
            enviando: t('inventario.recuentos.ficha.descartando'),
          }}
          ocupada={ocupada}
          enviar={(motivo, clave) => descartarRecuento({ id: recuento.id, version, clave, motivo })}
          alHacer={(hecho) => alHacer('descartado', hecho)}
          alFallar={alFallar}
        />
      )}
    </div>
  );
}

/**
 * Confirmar, en dos pasos: el botón abre la pregunta, con el foco en «Cancelar», porque lo que se
 * confirma genera un ajuste y no se hace con un Intro.
 */
function Confirmacion({
  recuento,
  version,
  ocupada,
  alHacer,
  alFallar,
}: {
  recuento: Recuento;
  version: string;
  ocupada: boolean;
  alHacer: (clase: 'confirmado', recuento: Recuento) => Promise<void>;
  alFallar: (error: unknown) => Promise<void>;
}): React.JSX.Element {
  const { t } = useTranslation();
  const [abierta, setAbierta] = useState(false);
  const boton = useRef<HTMLButtonElement>(null);
  const cancelar = useRef<HTMLButtonElement>(null);
  // Al cerrar la pregunta sin confirmar, el foco vuelve al botón que la abrió. Tras confirmar, o
  // tras no poder, va al aviso.
  const devolverElFoco = useRef(false);
  // En una referencia: el doble clic tiene que verla ya. Lo que compara es lo que viaja: la versión
  // y la huella. Tras un `409`, la ficha nueva trae otra huella, y volver a confirmar estrena clave.
  const intento = useRef<Intento | null>(null);

  useEffect(() => {
    if (abierta) {
      cancelar.current?.focus();
    } else if (devolverElFoco.current) {
      devolverElFoco.current = false;
      boton.current?.focus();
    }
  }, [abierta]);

  const cerrar = (): void => {
    devolverElFoco.current = true;
    setAbierta(false);
  };

  const confirmacion = useMutation({
    mutationFn: () => {
      const huellaDelTeorico = recuento.huellaDelTeorico ?? '';
      intento.current = intentoPara({ version, huellaDelTeorico }, intento.current);

      return confirmarRecuento({
        id: recuento.id,
        version,
        clave: intento.current.clave,
        huellaDelTeorico,
      });
    },
    onSuccess: async (hecho) => {
      intento.current = null;
      setAbierta(false);
      await alHacer('confirmado', hecho);
    },
    onError: async (error) => {
      setAbierta(false);
      await alFallar(error);
    },
  });

  const alPulsar = (evento: React.KeyboardEvent): void => {
    if (evento.key === 'Escape' && !confirmacion.isPending) {
      cerrar();
    }
  };

  if (!abierta) {
    return (
      <button
        type="button"
        ref={boton}
        disabled={ocupada}
        onClick={() => {
          setAbierta(true);
        }}
        className="rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50"
      >
        {t('inventario.recuentos.ficha.confirmar')}
      </button>
    );
  }

  return (
    <div
      role="group"
      aria-labelledby="pregunta-de-confirmacion"
      className="flex flex-wrap items-center gap-2 text-sm"
    >
      <span id="pregunta-de-confirmacion">{t('inventario.recuentos.ficha.preguntaConfirmar')}</span>
      <button
        type="button"
        disabled={confirmacion.isPending || ocupada}
        onClick={() => {
          confirmacion.mutate();
        }}
        onKeyDown={alPulsar}
        className="rounded bg-neutral-900 px-3 py-1.5 text-white disabled:opacity-50"
      >
        {confirmacion.isPending
          ? t('inventario.recuentos.ficha.confirmando')
          : t('inventario.recuentos.ficha.siConfirmar')}
      </button>
      <button
        type="button"
        ref={cancelar}
        disabled={confirmacion.isPending}
        onClick={cerrar}
        onKeyDown={alPulsar}
        className="rounded border border-neutral-300 px-3 py-1.5 disabled:opacity-50"
      >
        {t('inventario.recuentos.ficha.cancelar')}
      </button>
    </div>
  );
}

/**
 * Anular o descartar: el botón abre el motivo, que es obligatorio y queda en el recuento. El foco
 * va al campo; Escape y «Cancelar» lo cierran sin hacer nada.
 */
function CierreConMotivo({
  id,
  textos,
  ocupada,
  enviar,
  alHacer,
  alFallar,
}: {
  id: string;
  textos: { abrir: string; pregunta: string; si: string; enviando: string };
  ocupada: boolean;
  enviar: (motivo: string, clave: string) => Promise<Recuento>;
  alHacer: (recuento: Recuento) => Promise<void>;
  alFallar: (error: unknown) => Promise<void>;
}): React.JSX.Element {
  const [abierta, setAbierta] = useState(false);
  const boton = useRef<HTMLButtonElement>(null);
  // Al cerrar sin hacer nada, el foco vuelve al botón que abrió el motivo. Tras hacerlo, o tras no
  // poder, va al aviso.
  const devolverElFoco = useRef(false);

  useEffect(() => {
    if (!abierta && devolverElFoco.current) {
      devolverElFoco.current = false;
      boton.current?.focus();
    }
  }, [abierta]);

  if (!abierta) {
    return (
      <button
        type="button"
        ref={boton}
        disabled={ocupada}
        onClick={() => {
          setAbierta(true);
        }}
        className="rounded border border-red-800 px-4 py-2 text-sm text-red-900 disabled:opacity-50"
      >
        {textos.abrir}
      </button>
    );
  }

  return (
    <FormularioDeMotivo
      id={id}
      textos={textos}
      ocupada={ocupada}
      enviar={enviar}
      alCancelar={() => {
        devolverElFoco.current = true;
        setAbierta(false);
      }}
      alHacer={async (hecho) => {
        setAbierta(false);
        await alHacer(hecho);
      }}
      alFallar={async (error) => {
        setAbierta(false);
        await alFallar(error);
      }}
    />
  );
}

function FormularioDeMotivo({
  id,
  textos,
  ocupada,
  enviar,
  alCancelar,
  alHacer,
  alFallar,
}: {
  id: string;
  textos: { pregunta: string; si: string; enviando: string };
  ocupada: boolean;
  enviar: (motivo: string, clave: string) => Promise<Recuento>;
  alCancelar: () => void;
  alHacer: (recuento: Recuento) => Promise<void>;
  alFallar: (error: unknown) => Promise<void>;
}): React.JSX.Element {
  const { t } = useTranslation();
  // Se monta al abrir la pregunta y se desmonta al cerrarla: cada vez, con su intento por estrenar.
  const intento = useRef<Intento | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<DatosDeMotivo>({
    resolver: zodResolver(esquemaDeMotivo),
    defaultValues: { motivo: '' },
  });

  useEffect(() => {
    setFocus('motivo');
  }, [setFocus]);

  const hacer = async ({ motivo }: DatosDeMotivo): Promise<void> => {
    intento.current = intentoPara({ motivo }, intento.current);

    let hecho: Recuento;

    try {
      hecho = await enviar(motivo, intento.current.clave);
    } catch (error) {
      if (tipoDeFallo(error) === RECHAZO_DEL_MOTIVO) {
        setError(
          'motivo',
          { type: 'servidor', message: RECHAZO_DEL_MOTIVO },
          { shouldFocus: true },
        );
      } else {
        await alFallar(error);
      }

      return;
    }

    await alHacer(hecho);
  };

  const error =
    errors.motivo?.message === undefined ? null : t(`errores.tipos.${RECHAZO_DEL_MOTIVO}`);
  const campo = `motivo-de-${id}`;

  const alPulsar = (evento: React.KeyboardEvent): void => {
    if (evento.key === 'Escape' && !isSubmitting) {
      alCancelar();
    }
  };

  return (
    <form
      noValidate
      onSubmit={(evento) => {
        void handleSubmit(hacer)(evento);
      }}
      aria-labelledby={`pregunta-de-${id}`}
      className="flex max-w-md flex-col gap-2 text-sm"
    >
      <p id={`pregunta-de-${id}`}>{textos.pregunta}</p>
      <label htmlFor={campo} className="font-medium">
        {t('inventario.recuentos.motivo')}
      </label>
      <textarea
        id={campo}
        rows={2}
        aria-invalid={error !== null}
        aria-describedby={error === null ? undefined : `${campo}-error`}
        onKeyDown={alPulsar}
        {...register('motivo')}
        className="rounded border border-neutral-300 px-2 py-1.5"
      />
      {error !== null && (
        <p id={`${campo}-error`} role="alert" className="text-red-800">
          {error}
        </p>
      )}
      <div className="flex flex-wrap gap-2">
        <button
          type="submit"
          disabled={isSubmitting || ocupada}
          onKeyDown={alPulsar}
          className="rounded bg-red-800 px-3 py-1.5 text-white disabled:opacity-50"
        >
          {isSubmitting ? textos.enviando : textos.si}
        </button>
        <button
          type="button"
          disabled={isSubmitting}
          onClick={alCancelar}
          onKeyDown={alPulsar}
          className="rounded border border-neutral-300 px-3 py-1.5 disabled:opacity-50"
        >
          {t('inventario.recuentos.ficha.cancelar')}
        </button>
      </div>
    </form>
  );
}
