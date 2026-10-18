/**
 * Ejecuta `caso` con la zona horaria de quien mira fijada, y deja la que había.
 *
 * El caso fija su zona y no la hereda: en la del *runner* de la CI, que es UTC, el día local y el
 * UTC son el mismo, y un «hoy» sacado del calendario local pasaría (la mutación 541, verde con
 * `TZ=UTC` y roja en Madrid). Node vuelve a leer la zona cuando cambia `process.env.TZ`.
 *
 * Vive en `pruebas/` desde el 2.16, cuando el «hoy» pasó a ser uno y lo prueban dos ficheros: el
 * suyo y el de la pantalla de tarifas, que lo ejerce pintando.
 */
export async function enLaZona(zona: string, caso: () => void | Promise<void>): Promise<void> {
  const anterior = process.env['TZ'];
  process.env['TZ'] = zona;

  try {
    await caso();
  } finally {
    if (anterior === undefined) {
      delete process.env['TZ'];
    } else {
      process.env['TZ'] = anterior;
    }
  }
}
