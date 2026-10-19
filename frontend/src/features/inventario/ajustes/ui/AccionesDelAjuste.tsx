import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { anularAjuste, confirmarAjuste, descartarAjuste } from '../api/ajustes.ts';
import { accionesDisponibles } from '../model/acciones.ts';
import type { Anulacion, FichaDeAjuste, ResumenDeAjuste } from '../model/ajuste.ts';
import { esquemaDeMotivo, type DatosDeMotivo } from '../model/formularios.ts';
import { RECHAZO_DEL_MOTIVO } from '../model/rechazos.ts';
import { tipoDeFallo } from '@/shared/api/errores.ts';
import { intentoPara, type Intento } from '@/shared/api/intento.ts';
import { useSesionAbierta } from '@/shared/sesion/useSesion.ts';

/** Lo que la ficha hace al acabar una acción. */
export interface AlAcabar {
  readonly alConfirmar: (hecho: ResumenDeAjuste) => Promise<void>;
  readonly alAnular: (hecho: Anulacion) => Promise<void>;
  readonly alDescartar: () => Promise<void>;
  readonly alFallar: (error: unknown) => Promise<void>;
}

/**
 * Confirmar, descartar y anular, cada uno con su permiso y en el estado que lo admite
 * (`accionesDisponibles`, ADR-0065 §8).
 *
 * - **Confirmar y anular mandan su `Idempotency-Key`**, que es obligatoria, y no el `If-Match`: lo
 *   que la ficha enseña de un borrador solo cambia de estado, y eso lo mira el servidor (§7).
 * - **Descartar manda la versión de la ficha** en el `If-Match`, y se pregunta antes, porque no se
 *   puede deshacer desde la pantalla.
 *
 * Mientras la ficha se está leyendo otra vez, los botones esperan: con la versión de antes, el
 * descarte recibiría un `412` que nadie ha provocado.
 */
export function AccionesDelAjuste({
  ficha,
  ocupada,
  acabar,
}: {
  ficha: FichaDeAjuste;
  /** Si la ficha se está leyendo otra vez: hasta que llega, no se ofrece nada. */
  ocupada: boolean;
  acabar: AlAcabar;
}): React.JSX.Element | null {
  const { t } = useTranslation();
  const sesion = useSesionAbierta();
  const { ajuste, version } = ficha;
  const acciones = accionesDisponibles(ajuste, sesion.permisos);
  // En una referencia: el doble clic tiene que verla ya. Tras confirmar, la ficha cambia de estado y
  // el botón desaparece; tras no poder, repetir es la misma operación, con la misma clave.
  const intento = useRef<Intento | null>(null);

  if (acciones.length === 0) {
    return null;
  }

  return (
    <div className="mt-8 flex flex-col items-start gap-4">
      {acciones.includes('confirmar') && (
        <Pregunta
          id="confirmacion"
          textos={{
            abrir: t('inventario.ajustes.ficha.confirmar'),
            pregunta: t('inventario.ajustes.ficha.preguntaConfirmar'),
            si: t('inventario.ajustes.ficha.siConfirmar'),
            enviando: t('inventario.ajustes.ficha.confirmando'),
          }}
          peligrosa={false}
          ocupada={ocupada}
          hacer={() => {
            intento.current = intentoPara({ confirmar: ajuste.id }, intento.current);

            return confirmarAjuste(ajuste.id, intento.current.clave);
          }}
          alHacer={async (hecho) => {
            intento.current = null;
            await acabar.alConfirmar(hecho);
          }}
          alFallar={acabar.alFallar}
        />
      )}

      {acciones.includes('descartar') && (
        <Pregunta
          id="descarte"
          textos={{
            abrir: t('inventario.ajustes.ficha.descartar'),
            pregunta: t('inventario.ajustes.ficha.preguntaDescartar'),
            si: t('inventario.ajustes.ficha.siDescartar'),
            enviando: t('inventario.ajustes.ficha.descartando'),
          }}
          peligrosa
          ocupada={ocupada}
          hacer={() => descartarAjuste(ajuste.id, version)}
          alHacer={acabar.alDescartar}
          alFallar={acabar.alFallar}
        />
      )}

      {acciones.includes('anular') && (
        <Anular ajusteId={ajuste.id} ocupada={ocupada} acabar={acabar} />
      )}
    </div>
  );
}

/**
 * Una acción en dos pasos: el botón abre la pregunta, con el foco en «Cancelar», porque lo que se
 * pregunta no se hace con un Intro. Escape y «Cancelar» la cierran y devuelven el foco al botón.
 */
function Pregunta<T>({
  id,
  textos,
  peligrosa,
  ocupada,
  hacer,
  alHacer,
  alFallar,
}: {
  id: string;
  textos: { abrir: string; pregunta: string; si: string; enviando: string };
  peligrosa: boolean;
  ocupada: boolean;
  hacer: () => Promise<T>;
  alHacer: (hecho: T) => Promise<void>;
  alFallar: (error: unknown) => Promise<void>;
}): React.JSX.Element {
  const { t } = useTranslation();
  const [abierta, setAbierta] = useState(false);
  const boton = useRef<HTMLButtonElement>(null);
  const cancelar = useRef<HTMLButtonElement>(null);
  // Al cerrar la pregunta sin hacer nada, el foco vuelve al botón que la abrió. Tras hacerlo, o
  // tras no poder, va al aviso.
  const devolverElFoco = useRef(false);

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

  const accion = useMutation({
    mutationFn: hacer,
    onSuccess: async (hecho) => {
      setAbierta(false);
      await alHacer(hecho);
    },
    onError: async (error) => {
      setAbierta(false);
      await alFallar(error);
    },
  });

  const alPulsar = (evento: React.KeyboardEvent): void => {
    if (evento.key === 'Escape' && !accion.isPending) {
      cerrar();
    }
  };

  const fuerte = peligrosa ? 'bg-red-800' : 'bg-neutral-900';

  if (!abierta) {
    return (
      <button
        type="button"
        ref={boton}
        disabled={ocupada}
        onClick={() => {
          setAbierta(true);
        }}
        className={
          peligrosa
            ? 'rounded border border-red-800 px-4 py-2 text-sm text-red-900 disabled:opacity-50'
            : 'rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50'
        }
      >
        {textos.abrir}
      </button>
    );
  }

  return (
    <div
      role="group"
      aria-labelledby={`pregunta-de-${id}`}
      className="flex flex-wrap items-center gap-2 text-sm"
    >
      <span id={`pregunta-de-${id}`}>{textos.pregunta}</span>
      <button
        type="button"
        disabled={accion.isPending || ocupada}
        onClick={() => {
          accion.mutate();
        }}
        onKeyDown={alPulsar}
        className={`rounded ${fuerte} px-3 py-1.5 text-white disabled:opacity-50`}
      >
        {accion.isPending ? textos.enviando : textos.si}
      </button>
      <button
        type="button"
        ref={cancelar}
        disabled={accion.isPending}
        onClick={cerrar}
        onKeyDown={alPulsar}
        className="rounded border border-neutral-300 px-3 py-1.5 disabled:opacity-50"
      >
        {t('inventario.ajustes.ficha.cancelar')}
      </button>
    </div>
  );
}

/**
 * Anular: el botón abre el motivo, que es obligatorio y queda en el inverso. El foco va al campo;
 * Escape y «Cancelar» lo cierran sin hacer nada.
 */
function Anular({
  ajusteId,
  ocupada,
  acabar,
}: {
  ajusteId: string;
  ocupada: boolean;
  acabar: AlAcabar;
}): React.JSX.Element {
  const { t } = useTranslation();
  const [abierta, setAbierta] = useState(false);
  const boton = useRef<HTMLButtonElement>(null);
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
        {t('inventario.ajustes.ficha.anular')}
      </button>
    );
  }

  return (
    <FormularioDeMotivo
      ajusteId={ajusteId}
      ocupada={ocupada}
      alCancelar={() => {
        devolverElFoco.current = true;
        setAbierta(false);
      }}
      alHacer={async (hecho) => {
        setAbierta(false);
        await acabar.alAnular(hecho);
      }}
      alFallar={async (error) => {
        setAbierta(false);
        await acabar.alFallar(error);
      }}
    />
  );
}

function FormularioDeMotivo({
  ajusteId,
  ocupada,
  alCancelar,
  alHacer,
  alFallar,
}: {
  ajusteId: string;
  ocupada: boolean;
  alCancelar: () => void;
  alHacer: (hecho: Anulacion) => Promise<void>;
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

    let hecho: Anulacion;

    try {
      hecho = await anularAjuste(ajusteId, motivo, intento.current.clave);
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
  const campo = 'motivo-de-la-anulacion';

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
      aria-labelledby="pregunta-de-anulacion"
      className="flex max-w-md flex-col gap-2 text-sm"
    >
      <p id="pregunta-de-anulacion">{t('inventario.ajustes.ficha.preguntaAnular')}</p>
      <label htmlFor={campo} className="font-medium">
        {t('inventario.ajustes.motivo')}
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
          {isSubmitting
            ? t('inventario.ajustes.ficha.anulando')
            : t('inventario.ajustes.ficha.siAnular')}
        </button>
        <button
          type="button"
          disabled={isSubmitting}
          onClick={alCancelar}
          onKeyDown={alPulsar}
          className="rounded border border-neutral-300 px-3 py-1.5 disabled:opacity-50"
        >
          {t('inventario.ajustes.ficha.cancelar')}
        </button>
      </div>
    </form>
  );
}
