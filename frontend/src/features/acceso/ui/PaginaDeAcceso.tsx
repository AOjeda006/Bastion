import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Navigate, useLocation, useNavigate } from 'react-router';

import { destinoSeguro, esquemaDeAcceso, type DatosDeAcceso } from '../model/esquemaDeAcceso.ts';
import type { Diccionario } from '@/app/i18n/es.ts';
import { iniciarSesion, type MotivoDeAcceso } from '@/shared/api/sesiones.ts';
import { escribirSesion } from '@/shared/sesion/deposito.ts';
import { useSesion } from '@/shared/sesion/useSesion.ts';

/**
 * Las claves que el esquema de Zod puede poner en `message`. La aserción de abajo va contra ESTE
 * tipo y no contra `string`: React Hook Form tipa `message` como `string | undefined` porque no
 * sabe que aquí guardamos claves, y sin el tipo la aserción dejaría pasar cualquier cosa.
 */
type ClaveDeMensajeDeAcceso = `acceso.${keyof Diccionario['acceso']}`;

/**
 * La puerta.
 *
 * Componentes no controlados con React Hook Form y validación con Zod: el formulario no redibuja en
 * cada tecla, y el esquema es el único sitio donde están escritas las reglas.
 *
 * De accesibilidad, lo que exige `ux-ipo`: etiqueta visible asociada por programación —el
 * `placeholder` NO es una etiqueta—, el error en texto y no solo en rojo, asociado al campo con
 * `aria-describedby`, y el campo marcado con `aria-invalid`. Y `autoComplete`, que es lo que deja
 * trabajar al gestor de contraseñas (3.3.8: autenticación sin prueba cognitiva).
 */
export function PaginaDeAcceso(): React.JSX.Element {
  const { t } = useTranslation();
  const sesion = useSesion();
  const ubicacion = useLocation();
  const navegar = useNavigate();
  const cache = useQueryClient();

  const [motivo, setMotivo] = useState<MotivoDeAcceso | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<DatosDeAcceso>({ resolver: zodResolver(esquemaDeAcceso) });

  const destino = destinoSeguro(ubicacion.state);

  if (sesion !== null) {
    // Ya se está dentro: quedarse aquí ofrecería entrar otra vez, y entrar otra vez emitiría una
    // familia de testigos nueva dejando la anterior viva.
    return <Navigate to={destino} replace />;
  }

  const entrar = async (datos: DatosDeAcceso): Promise<void> => {
    setMotivo(null);

    const resultado = await iniciarSesion(datos.correo, datos.contrasena);

    if ('error' in resultado) {
      setMotivo(resultado.error);
      return;
    }

    // La caché se vacía también AL ENTRAR, no solo al cambiar de empresa: en esta pestaña puede
    // quedar lo que consultó quien estuviera antes. Aquí `clear()` basta y `resetQueries()` sobra:
    // no hay ninguna pantalla de datos montada a la que haya que hacer volver a pedir.
    escribirSesion(resultado.sesion);
    cache.clear();

    await navegar(destino, { replace: true });
  };

  return (
    <form
      noValidate
      onSubmit={(evento) => {
        void handleSubmit(entrar)(evento);
      }}
      className="mt-6 max-w-sm space-y-4"
    >
      {motivo !== null && (
        <p
          role="alert"
          className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900"
        >
          {t(`acceso.${motivo}`)}
        </p>
      )}

      <div>
        <label htmlFor="correo" className="block text-sm font-medium">
          {t('acceso.correo')}
        </label>
        <input
          id="correo"
          type="email"
          autoComplete="username"
          aria-invalid={errors.correo !== undefined}
          aria-describedby={errors.correo !== undefined ? 'correo-error' : undefined}
          {...register('correo')}
          className="mt-1 w-full rounded border border-neutral-300 px-3 py-2"
        />
        {errors.correo !== undefined && (
          <p id="correo-error" className="mt-1 text-sm text-red-800">
            {t(errors.correo.message as ClaveDeMensajeDeAcceso)}
          </p>
        )}
      </div>

      <div>
        <label htmlFor="contrasena" className="block text-sm font-medium">
          {t('acceso.contrasena')}
        </label>
        <input
          id="contrasena"
          type="password"
          autoComplete="current-password"
          aria-invalid={errors.contrasena !== undefined}
          aria-describedby={errors.contrasena !== undefined ? 'contrasena-error' : undefined}
          {...register('contrasena')}
          className="mt-1 w-full rounded border border-neutral-300 px-3 py-2"
        />
        {errors.contrasena !== undefined && (
          <p id="contrasena-error" className="mt-1 text-sm text-red-800">
            {t(errors.contrasena.message as ClaveDeMensajeDeAcceso)}
          </p>
        )}
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className="rounded bg-neutral-900 px-4 py-2 text-sm text-white disabled:opacity-50"
      >
        {isSubmitting ? t('acceso.entrando') : t('acceso.entrar')}
      </button>
    </form>
  );
}
