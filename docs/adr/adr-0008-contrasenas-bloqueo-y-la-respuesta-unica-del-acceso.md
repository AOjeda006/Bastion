---
tipo: referencia
stack: [dotnet, aspnetcore]
aplica_a: [autenticacion, seguridad, criptografia, manejo-errores]
revisado: 2026-08-26
tags: [adr, contrasenas, pbkdf2, bloqueo, enumeracion-de-usuarios, jwt]
---

# ADR-0008: Contraseñas, bloqueo y la respuesta única del acceso

- **Estado:** aceptado
- **Fecha:** 2026-08-26

## Contexto

El ítem 0.5 monta la puerta de entrada al sistema. Tres decisiones de ese ítem no se ven leyendo
el código —parecen detalles de implementación— y las tres son difíciles de cambiar después:

1. **Con qué se resumen las contraseñas.** Cambiar de algoritmo con cuentas ya dadas de alta no es
   un `sed`: hay que poder verificar con el viejo mientras se re-resume con el nuevo.
2. **Cuándo se le cierra la puerta a una cuenta.** Un tope mal puesto o deja pasar la fuerza bruta,
   o convierte el formulario en un botón de «denegar el servicio a quien yo diga».
3. **Qué se contesta cuando el acceso falla.** Un mensaje distinto para «no existe» y para
   «contraseña incorrecta» convierte el formulario de acceso en una consulta de padrón.

La instrucción del encargo era explícita: *no inventes criptografía; el hasher de Identity vale,
pero **escribe cuál y con qué parámetros**, junto con el bloqueo por intentos*. Esto es eso.

## Decisión

### 1. PBKDF2-HMAC-SHA512, 100 000 iteraciones, y no una línea de criptografía propia

Se adopta `Microsoft.AspNetCore.Identity.PasswordHasher<T>` **con sus parámetros por omisión**
(`PasswordHasherCompatibilityMode.IdentityV3`), que hoy son:

| Parámetro | Valor |
|---|---|
| Función de derivación | PBKDF2 (RFC 2898) |
| PRF | HMAC-SHA512 |
| Iteraciones | 100 000 |
| Sal | 128 bits, aleatoria por contraseña |
| Clave derivada | 256 bits |
| Formato | un solo `varchar`: marca de versión + iteraciones + sal + clave, en Base64 |

Tres cosas justifican adoptarlo en vez de escribirlo:

- **Los parámetros viajan dentro del resumen.** Subir el coste no obliga a una columna nueva ni a
  una migración: los resúmenes viejos siguen diciendo con qué se calcularon.
- **La comparación es en tiempo constante** y la sal es por contraseña. Los dos son fáciles de
  escribir mal y el fallo no se ve en ningún test funcional.
- **Es código mantenido por quien mantiene la plataforma.** Cuando .NET suba el coste por omisión,
  llega con la actualización del paquete.

Se adopta **solo el hasher**, no ASP.NET Core Identity entero: su modelo de datos no admite
pertenencias por empresa ni permisos por acción sin retorcerlo, y arrastrarlo habría metido su
esquema en el nuestro.

**PBKDF2 no es la mejor elección posible en 2026** —Argon2id resiste mejor el hardware
especializado—, pero la mejor elección posible exige un paquete de terceros, elegir tres parámetros
de memoria/paralelismo/tiempo sin datos del hardware de destino, y mantenerlos. Lo que hace
aceptable PBKDF2 aquí es lo del punto siguiente: la vía de migración ya está montada y probada.

### 2. El re-resumen oportunista, o subir el coste no sirve de nada

`Comprobar` distingue tres desenlaces y no dos: correcta, **correcta pero conviene rehashear**, e
incorrecta. El segundo es el aviso del hasher de que ese resumen se calculó con una versión o un
coste anteriores.

El inicio de sesión actúa sobre ese aviso: re-resume la contraseña **en el único instante en que
está disponible en claro** y guarda el resultado en la misma transacción. Sin esto, subir el coste
solo protegería a las cuentas creadas después del cambio, que son justo las que menos falta hacía
proteger. Con esto, cada cuenta se actualiza sola la próxima vez que su dueño entra.

### 3. Bloqueo por intentos: 5 y 15 minutos, temporal y sobre la cuenta

- `Usuario.IntentosTolerados = 5`
- `Usuario.EsperaTrasIntentosFallidos = 15 min`

Y **dos campos distintos**, que es la parte que importa:

- `Estado = Bloqueado` (+ `BloqueadoEn`) es la baja administrativa (R16). La decide una persona y
  no caduca sola.
- `RechazadoHasta` es el rechazo por intentos fallidos. Es automático, temporal y se levanta solo.

Mezclarlos en un campo tiene una de dos consecuencias, las dos malas: o un ataque de fuerza bruta
da de baja la cuenta —que es exactamente el favor que el atacante venía a pedir—, o una baja
administrativa caduca al cuarto de hora.

El contador solo sube **cuando existe la cuenta**. Si no existe no hay nada que bloquear: el
rechazo protege una cuenta concreta, no el formulario. Un acceso correcto lo pone a cero.

15 minutos, y no una hora, porque quien se bloquea de verdad es casi siempre el dueño de la cuenta
escribiendo mal su propia contraseña; y no 30 segundos, porque entonces no frena nada.

### 4. Una sola salida para los seis fallos del acceso

Correo con forma imposible, cuenta que no existe, contraseña incorrecta, cuenta dada de baja,
cuenta rechazando intentos y usuario sin ninguna pertenencia devuelven **el mismo código y el mismo
texto**: `ErroresDeSesion.Credenciales()`.

Y no solo el mismo cuerpo — también el mismo **trabajo**:

- Cuando no hay usuario, se comprueba la contraseña igual, contra un `HashDeRelleno` calculado al
  arrancar sobre una contraseña aleatoria que no se guarda en ninguna parte. Sin eso, «no existe»
  contesta en microsegundos y «contraseña incorrecta» en los ~100 ms que cuesta PBKDF2: la
  diferencia se mide con `curl` y responde la pregunta que el cuerpo se niega a responder.
- El estado de la cuenta se mira **después** de comprobar la contraseña, no antes, por lo mismo.

Esto es el caso concreto de la regla general del encargo: *el de fuera necesita saber qué hacer, el
de dentro qué ha pasado; no se juntan*. Quien está delante del formulario necesita saber que tiene
que volver a intentarlo — nada más. Lo que ha pasado de verdad va al registro del servidor.

## Consecuencias

- **Coste asumido:** cada intento de acceso —incluidos los que van a fallar— cuesta un PBKDF2 de
  100 000 iteraciones. Es deliberado y es el precio del punto 4.
- El día que se suba el coste o se cambie de algoritmo, la vía es: cambiar el
  `PasswordHasherOptions` (o la implementación de `IHasherDeContrasenas`), dejar que
  `SuccessRehashNeeded` haga el resto, y no tocar ninguna fila a mano.
- **Lo que este ADR NO cubre:** el token de acceso y el de refresco (duración, rotación, detección
  de reutilización y la cookie `__Host-`) están descritos en el §11 del plan maestro y verificados
  en `tests/Api.IntegrationTests/Acceso/SesionesYTokensTests.cs`. Aquí solo está lo que se guarda
  en la columna `hash_de_contrasena` y lo que decide si la puerta se abre.

## Cómo se comprueba

Por el efecto, no leyendo esta página:

| Regla | Dónde se ejercita |
|---|---|
| Un correo que no existe y una contraseña mala contestan lo mismo | `El_correo_que_no_existe_y_la_contrasena_mala_dan_la_MISMA_respuesta` |
| Cinco fallos rechazan la cuenta, y el sexto no pasa ni con la buena | `Tras_cinco_intentos_fallidos_la_cuenta_no_admite_ni_la_contrasena_buena` |
| El resumen no se guarda en claro ni es reversible | `EsquemaDeIdentidadTests` + `UsuarioTests` |
| Nada del interior sale en la respuesta de un acceso hostil | `Un_correo_hostil_en_el_inicio_de_sesion_no_cuenta_nada_de_dentro` |
