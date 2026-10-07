using System.Reflection;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Idempotencia;

/// <summary>
/// Toda acción que cambia estado dice cómo se protege del reintento: exige <c>If-Match</c>, admite
/// <c>Idempotency-Key</c>, o está en la lista de exentas con su motivo escrito.
/// </summary>
/// <remarks>
/// <para>
/// <b>Son dos mecanismos distintos, no dos niveles del mismo.</b> El <c>If-Match</c> protege de que
/// dos personas pisen el mismo recurso: la segunda escritura llega con una versión que ya no es la
/// actual y se la lleva un <c>412</c>. La <c>Idempotency-Key</c> protege de que <b>una</b> repita su
/// propia petición: el segundo intento devuelve la respuesta del primero sin volver a hacer el
/// trabajo. Una alta no tiene versión previa que citar —no existía—, así que solo la segunda la
/// protege; una modificación ya la trae de la lectura, así que le basta la primera.
/// </para>
/// <para>
/// <b>Por qué un barrido y no una lista en la documentación.</b> Una acción nueva sin ninguna de
/// las dos cosas no rompe ningún test: funciona. Lo que pasa es que el día que un cliente reintente
/// —y los clientes reintentan: un móvil que pierde la cobertura al enviar reintenta solo— duplicará
/// un alta o pisará el trabajo de otro. Ese fallo no tiene síntoma en desarrollo. Aquí lo tiene.
/// </para>
/// <para>
/// <b>Se comparan las listas ENTERAS</b>, en los dos sentidos: una exención que sobra es un permiso
/// que sigue concedido sobre una acción que ya cambió, y el siguiente que la toque no verá ningún
/// rojo.
/// </para>
/// </remarks>
public sealed class TodaEscrituraDiceComoSeProtegeTests : IDisposable
{
    // Las que cambian estado y NO llevan ninguno de los dos mecanismos, con el motivo de cada una.
    // Cada línea es una decisión: la exención se gana con el argumento, no por ser incómoda de
    // arreglar.
    private static readonly Dictionary<string, string> s_exentas = new(StringComparer.Ordinal)
    {
        ["SesionesController.Iniciar"] =
            "es anónima por definición —viene a identificarse— así que no hay tupla (empresa, " +
            "usuario) con la que formar una clave, y su respuesta lleva credenciales dentro: " +
            "guardarla metería un token de acceso en una tabla. Repetirla no duplica nada: emite " +
            "otra sesión, que es lo que se le ha pedido",

        ["SesionesController.Renovar"] =
            "lo mismo, y con un motivo propio encima: el refresco YA es de un solo uso —la emisión " +
            "anterior se revoca al canjearla—, así que el segundo intento con el mismo token falla " +
            "por sí mismo. La protección está en el dominio, no en una cabecera",

        ["SesionesController.Cerrar"] =
            "cerrar una sesión ya cerrada es cerrarla: el estado final es el mismo se repita las " +
            "veces que se repita. Y es anónima a propósito, para poder cerrar con un token ya " +
            "caducado",

        ["SesionesController.CambiarEmpresa"] =
            "emite un token nuevo con otra empresa activa; repetirlo emite otro igual de válido y " +
            "no acumula nada. No se guarda su respuesta por lo mismo que la de Iniciar: lleva " +
            "credenciales",

        ["UsuariosController.CambiarContrasenaPropia"] =
            "fijar la misma contraseña dos veces deja el mismo estado. Y no exige If-Match porque " +
            "SU precondición ya viaja en el cuerpo: hay que presentar la contraseña de ahora, así " +
            "que si otro la cambió en medio la petición falla por ahí, que es exactamente lo que " +
            "un If-Match habría hecho",

        ["UsuariosController.Restablecer"] =
            "lo mismo por el lado del efecto: fijar la misma contraseña dos veces deja el mismo " +
            "estado. La cabecera no se admite porque su cuerpo ES la contraseña nueva, y una clave " +
            "de idempotencia invita a reintentar con el mismo cuerpo desde donde sea",

        ["UsuariosController.Conceder"] =
            "conceder una pertenencia que ya está concedida no la duplica: choca con su clave y " +
            "sale un 409. If-Match no vale AQUÍ y no es pereza: la fila que se toca no es la del " +
            "usuario, así que su versión no se comprobaría nunca — y una cabecera que parece " +
            "proteger sin proteger es peor que no tenerla",

        ["UsuariosController.Retirar"] =
            "retirar una pertenencia que ya no está es un 404, no un segundo efecto. Y el If-Match " +
            "sobre el usuario tampoco protegería esta fila, por lo mismo que en Conceder",

        ["UsuariosController.AsignarRol"] =
            "asignar un rol ya asignado choca con su clave; repetirlo no acumula. Mismo motivo que " +
            "Conceder para no exigir If-Match sobre el usuario",

        ["UsuariosController.RetirarRol"] =
            "retirar un rol que ya no está es un 404. Mismo motivo que Retirar",

        // Las tres siguientes entraron en esta lista en el 0.10, y no por comodidad: hasta el 0.9
        // exigían If-Match y lo perdieron porque R16 dejó su llave fuera del alcance del cliente.
        // El argumento entero está en el ADR-0017; aquí va el resumen, una vez por acción porque
        // cada una tiene su matiz.
        //
        // Y cada una NOMBRA LA CONDICIÓN DE LA QUE DEPENDE. Un motivo que solo explica por qué hoy
        // no hace falta la cabecera envejece en silencio: el día que la condición cambie, la
        // exención seguirá aquí pareciendo razonada, y nadie sabrá que dejó de serlo. Escrita la
        // condición, quien la cambie se encuentra con la frase que dice que esto caduca.
        //
        // EN EL ÍTEM 1.4 CADUCÓ, y esto es lo que pasa cuando eso ocurre. `GET .../bloqueados`
        // (ADR-0027) es una lectura de la API que entrega filas bloqueadas, así que la mitad
        // literal de estas frases -«que ninguna lectura de la API entregue X bloqueado»- dejó de
        // ser cierta. No se han borrado: se reescriben DICIENDO QUÉ LAS SUSTITUYE, porque una
        // condición que se borra deja la exención otra vez sin apoyo escrito y al siguiente que
        // lea esto sin manera de saber que hubo una.
        //
        // Y lo que las sustituye ya no es una frase. La condición que hoy sostiene las cuatro
        // -que ningún camino de lectura entregue un recurso bloqueado CON TESTIGO DE VERSIÓN- la
        // afirman dos reglas que se ponen rojas: `NingunaLecturaEntregaTestigoDeVersionTests`,
        // sobre el contrato entero de la API, y `Ningun_camino_que_ve_lo_bloqueado_emite_un_
        // testigo_de_version`, sobre el código que abre el ámbito. La prosa no falla; esas sí.
        ["EmpresasController.Desbloquear"] =
            "no puede exigir If-Match desde el 0.10: la etiqueta se obtiene leyendo el recurso, y " +
            "una empresa bloqueada contesta 404 a su propio GET. Una precondición cuya llave no " +
            "hay manera de conseguir no es una precondición, es un muro. Y no hace falta: " +
            "mientras está bloqueada ninguna otra escritura llega a la fila —todas la piden al " +
            "repositorio y el filtro no se la da—, así que no hay con quién competir; desbloquear " +
            "dos veces deja el mismo estado (ADR-0017). DEPENDE DE que ninguna lectura de la API " +
            "entregue una empresa bloqueada CON SU ETIQUETA. Hasta el 1.4 esto se decía más " +
            "ancho -que ninguna lectura entregara una empresa bloqueada, punto- y esa mitad " +
            "CADUCÓ ahí: `GET .../bloqueados` es exactamente eso (ADR-0027). Lo que la sustituye " +
            "es más estrecho y sigue siendo lo que importa, porque la llave que If-Match pediría " +
            "es la etiqueta y no la fila: ese listado no emite ninguna. Y ya no es una promesa " +
            "escrita, la afirman `NingunaLecturaEntregaTestigoDeVersionTests` y la regla de " +
            "caminos disjuntos de `ElFiltroNoSeSaltaPorAhiTests`. El día que una lectura de lo " +
            "bloqueado emita versión -una ficha individual, o un campo de más en el DTO del " +
            "listado-, esas dos se ponen rojas, esta exención caduca de verdad y hay que volver a " +
            "exigir If-Match aquí",

        ["AlmacenesController.Desbloquear"] =
            "lo mismo, y con la misma consecuencia: el almacén bloqueado no emite ETag porque no " +
            "se deja leer. El testigo de concurrencia SIGUE comprobándose dentro de la petición " +
            "—se lee la fila y se guarda en la misma transacción—; lo que desaparece es la " +
            "precondición que el cliente cita, no la protección (ADR-0017). DEPENDE DE lo mismo " +
            "que la de Empresas —que ninguna lectura de la API entregue un almacén bloqueado CON " +
            "SU ETIQUETA, después de que el 1.4 caducara la mitad ancha de esa frase— y ADEMÁS " +
            "de que el bloqueo de la empresa siga tapando a sus almacenes: si un almacén quedara " +
            "legible con etiqueta mientras su empresa está bloqueada, habría ETag y no habría " +
            "exención. La segunda mitad NO ha caducado y no la cubre ninguna de las dos reglas de " +
            "versión: la sostiene que el listado de lo bloqueado abre el ámbito del bloqueo y " +
            "ninguno más, así que el filtro de empresa sigue puesto ahí dentro (R8)",

        // La cuarta, del 0.15, y por el mismo argumento que las otras tres de Organización.
        ["UbicacionesController.Desbloquear"] =
            "igual que la del almacén: una ubicación bloqueada no se deja leer, así que no emite " +
            "ETag y la precondición pediría una llave que no se puede conseguir. El testigo de " +
            "concurrencia se sigue comprobando dentro de la petición; lo que desaparece es la " +
            "cabecera que el cliente cita (ADR-0017). DEPENDE DE lo mismo que la del almacén " +
            "—que ninguna lectura de la API entregue una ubicación bloqueada CON SU ETIQUETA, " +
            "que es lo que quedó de esa frase cuando el 1.4 le caducó la mitad ancha— y ADEMÁS " +
            "de que el bloqueo del almacén y el de la empresa sigan tapando lo que cuelga de " +
            "ellos",

        // La quinta no es un desbloqueo ni se le parece: es una BÚSQUEDA. Entró en el ítem 1.3
        // con el endpoint, no cuando el carril se puso rojo, que es lo que pedía el ADR-0025 —y
        // la diferencia importa: escrita con el carril en rojo, la tentación es ensanchar la
        // partición para que las búsquedas dejen de contar como escrituras, y esa partición por
        // verbo es correcta y no tiene falsos negativos.
        ["TercerosController.Desbloquear"] =
            "el cuarto por el mismo argumento del ADR-0017, y el primero en un módulo que aún no " +
            "tiene listado de lo bloqueado: un tercero bloqueado no se lee por ningún camino " +
            "ordinario, así que no hay etiqueta que citar y exigirla sería cerrar la puerta con " +
            "la llave dentro. DEPENDE DE lo mismo que las tres de Organización: de que ninguna " +
            "lectura entregue un tercero bloqueado CON SU ETIQUETA. Que hoy no exista " +
            "`GET .../terceros/bloqueados` no hace la exención más sólida, la hace menos " +
            "interesante; cuando ese listado se construya, lo que la sostenga será exactamente lo " +
            "que sostiene a las otras tres, y lo afirmarán las mismas dos reglas",

        ["EmpresasController.Buscar"] =
            "es un POST que NO cambia estado: lee. El verbo es POST porque el criterio —el NIF de " +
            "una empresa, que puede ser el DNI de un empresario individual— no puede viajar en la " +
            "cadena de consulta (ADR-0025), no porque cree nada. No hay recurso previo cuya " +
            "versión citar, así que If-Match no tiene qué exigir; y una clave de idempotencia " +
            "guardaría la RESPUESTA, o sea que metería datos personales en " +
            "`auditoria.claves_de_idempotencia` para ahorrar una consulta que no escribe nada. " +
            "Repetirla no acumula: devuelve lo mismo. DEPENDE DE que siga sin escribir: el día " +
            "que una búsqueda apunte algo —un registro de lo buscado, una lista reciente—, deja " +
            "de ser esto y la exención caduca",

        ["TercerosController.Buscar"] =
            "gemela de la de empresas y con más motivo: el criterio es el identificador fiscal de " +
            "un tercero, que muy a menudo ES el DNI de una persona física, y por la cadena de " +
            "consulta quedaría escrito en el historial, en el enlace que se copia y en el " +
            "registro de acceso del servidor de delante (ADR-0025). POST porque el criterio no " +
            "puede viajar en la URL, no porque cree nada. Una clave de idempotencia guardaría la " +
            "RESPUESTA, o sea que metería esos mismos datos en " +
            "`auditoria.claves_de_idempotencia`. DEPENDE DE que siga sin escribir: el día que la " +
            "búsqueda apunte algo —una lista de recientes, una traza de lo buscado— deja de ser " +
            "esto y la exención caduca",

        ["UsuariosController.Desbloquear"] =
            "igual que las dos de Organización. Y aquí conviene dejar escrito lo que NO es: esto " +
            "levanta el bloqueo del art. 32, no el rechazo temporal por intentos fallidos, que " +
            "vive en `rechazado_hasta`, se levanta solo y nunca sacó al usuario de las consultas " +
            "(ADR-0017). DEPENDE DE dos cosas, no de una: de que el usuario bloqueado siga sin " +
            "emitir etiqueta, y de que `rechazado_hasta` siga siendo un mecanismo aparte. La " +
            "primera mitad NO caducó en el 1.4 y conviene decir por qué: el acceso reservado del " +
            "art. 32 se construyó en Organización y lista empresas, almacenes y ubicaciones, no " +
            "usuarios, así que aquí sigue sin haber ninguna lectura. Aun así se dice ya en la " +
            "forma estrecha —sin etiqueta, en vez de sin lectura— porque el día que Identidad " +
            "tenga su listado de lo bloqueado, lo que sostenga esta exención será lo mismo que " +
            "sostiene las tres de Organización, y estará afirmado por las mismas dos reglas. Si " +
            "algún día el rechazo temporal se levantara por esta misma acción, la acción dejaría " +
            "de ser la de un solo efecto que aquí se describe",
    };

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Toda_accion_que_cambia_estado_dice_como_se_protege()
    {
        List<string> sinDecirlo =
        [
            .. QueCambianEstado()
                .Where(accion => !accion.ExigeVersion && !accion.AdmiteIdempotencia)
                .Select(accion => accion.Nombre)
                .Where(nombre => !s_exentas.ContainsKey(nombre)),
        ];

        sinDecirlo.ShouldBeEmpty(
            "estas acciones cambian estado y no dicen cómo se protegen del reintento: " +
            string.Join(", ", sinDecirlo));
    }

    [Fact]
    public void La_clave_obligatoria_es_la_excepcion_y_esta_declarada_entera()
    {
        List<string> obligatoriasDeVerdad =
            [.. Todas().Where(accion => accion.ExigeIdempotencia).Select(accion => accion.Nombre)];

        List<string> sinMotivo =
            [.. obligatoriasDeVerdad.Where(nombre => !s_obligatorias.ContainsKey(nombre))];

        sinMotivo.ShouldBeEmpty(
            "estas acciones EXIGEN la clave y no dicen por qué: " + string.Join(", ", sinMotivo));

        List<string> sobran =
            [.. s_obligatorias.Keys.Where(nombre => !obligatoriasDeVerdad.Contains(nombre))];

        sobran.ShouldBeEmpty(
            "estas acciones están declaradas como obligatorias y ya no lo son (o ya no existen): " +
            string.Join(", ", sobran));
    }

    [Fact]
    public void La_lista_de_exentas_no_nombra_acciones_que_ya_no_lo_estan()
    {
        HashSet<string> exentasDeVerdad =
        [
            .. QueCambianEstado()
                .Where(accion => !accion.ExigeVersion && !accion.AdmiteIdempotencia)
                .Select(accion => accion.Nombre),
        ];

        List<string> sobran = [.. s_exentas.Keys.Where(nombre => !exentasDeVerdad.Contains(nombre))];

        sobran.ShouldBeEmpty(
            "estas acciones están exentas y ya no lo necesitan (o ya no existen): " +
            string.Join(", ", sobran));
    }

    // Las que la EXIGEN, con el motivo de cada una. Es la lista de las excepciones a la doctrina
    // del 0.9 —«la clave es una garantía que el cliente PIDE, no un peaje que se le cobra»— y se
    // compara entera en los dos sentidos, igual que la de arriba: una obligatoriedad que sobra es
    // un peaje que se sigue cobrando sin que nadie recuerde por qué, y una que falta es una
    // excepción colada sin argumento.
    //
    // El criterio NO es «esto es importante» —todo lo es—: es que sin la cabecera la acción no
    // pueda cumplir lo que promete, porque el filtro se aparta sin abrir transacción y la
    // atomicidad se va con ella.
    private static readonly Dictionary<string, string> s_obligatorias = new(StringComparer.Ordinal)
    {
        ["AjustesController.Confirmar"] =
            "toma un número de serie, y el número y el documento tienen que quedar escritos en la " +
            "MISMA transacción. Sin cabecera no hay transacción: el UPDATE del contador se " +
            "confirmaría por su cuenta y un fallo posterior dejaría el número gastado sin " +
            "documento que lo llevara —un hueco, que es lo que la R5 prohibe—. La alternativa, " +
            "que el caso de uso abriera la suya cuando no la hay, deja dos dueños y dos semánticas " +
            "de fallo en el mismo endpoint según venga o no una cabecera",

        ["AjustesController.Anular"] =
            "el MISMO motivo que la confirmación, y no uno parecido: el documento que anula es un " +
            "ajuste confirmado de pleno derecho, así que toma su propio número de la misma serie " +
            "y tiene que quedar escrito con él en la MISMA transacción. Sin cabecera no hay " +
            "transacción y el mecanismo de numeración revienta antes de dar nada, que es su forma " +
            "de no dejar un hueco (R5). Que sean dos y no una es la primera vez que esta lista " +
            "crece, y por eso conviene decir qué NO la hace crecer: no entra por ser importante " +
            "—todo lo es—, entra porque sin la cabecera la acción no puede cumplir lo que promete",

        // Las tres de la transferencia, del 2.11 (ADR-0053 §9). Dos numeran, como las del ajuste; la
        // tercera no, y entra por la otra mitad del mismo criterio: lo que promete no cabe en una
        // sentencia suelta.
        ["TransferenciasController.Enviar"] =
            "el motivo de la confirmación del ajuste, entero: toma el número de su serie, y el " +
            "número, la salida del origen y lo que queda en tránsito tienen que quedar escritos en " +
            "la MISMA transacción. Sin ella el UPDATE del contador se confirmaría solo, y un fallo " +
            "posterior dejaría un hueco en la serie (R5)",

        ["TransferenciasController.Recibir"] =
            "no numera, y por eso es la que necesita el argumento escrito: bloquea la valoración del " +
            "destino y escribe el documento, el tránsito que baja, la existencia y el libro. Sin la " +
            "transacción del filtro, cada sentencia se confirmaría por su cuenta: el cerrojo se " +
            "soltaría al acabar la suya, y un fallo a mitad dejaría la mercancía fuera del tránsito " +
            "y sin entrar en el destino, que es la ventana que la R3 no admite",

        ["TransferenciasController.Anular"] =
            "el de la anulación del ajuste: el inverso toma su propio número de la serie del " +
            "original, y tiene que quedar escrito con él, con las patas que niega y con el original " +
            "anulado en la MISMA transacción (R5, R2)",

        // La del recuento, del 2.12 (ADR-0055 §3): el argumento de la confirmación, dos veces.
        ["RecuentosController.Confirmar"] =
            "toma DOS números, el del recuento y el de su ajuste si hay diferencias, y los dos, el " +
            "ajuste, su libro y el recuento confirmado tienen que quedar escritos en la MISMA " +
            "transacción. Sin ella, cada contador se confirmaría por su cuenta y un fallo posterior " +
            "dejaría dos huecos (R5)",

        ["RecuentosController.Anular"] =
            "el de la anulación del ajuste, porque es la misma: el inverso del ajuste del recuento " +
            "toma su número de la serie del ajuste, y tiene que quedar escrito con él, con su libro, " +
            "con el ajuste anulado y con el recuento anulado en la MISMA transacción (R5, R2, " +
            "ADR-0055 §9)",
    };

    // Las que piden los DOS mecanismos, con el motivo de cada una (ADR-0057). Hasta el 2.12 no había
    // ninguna, y la regla las prohibía por dos razones:
    //
    // 1. La repetición devolvería una respuesta con el ETag de entonces, que ya no sería el actual.
    // 2. La transacción de la idempotencia va SIN puntos de guardado (ver AlmacenDeIdempotencia), y
    //    un choque de concurrencia la dejaría abortada: el manejador del 412 consulta la versión
    //    actual de la fila para ponerla en la respuesta, y esa consulta fallaría. El cliente
    //    recibiría un 500 donde tocaba un 412, y lo reintentaría, que es lo contrario de lo que
    //    hay que hacer con un choque.
    //
    // Una acción entra aquí solo si contesta a las dos. A la primera, respondiendo SIN ETag: la
    // versión nueva se lee con la ficha. A la segunda, comparando la versión con la fila ya
    // bloqueada y devolviendo el 412 como un resultado, antes de escribir nada, así que el testigo
    // de EF Core no llega a chocar. Lo afirman sus casos de integración: la respuesta y su
    // repetición sin ETag, el If-Match viejo y las dos confirmaciones a la vez, que dan 412 y no 500.
    private static readonly Dictionary<string, string> s_conLosDos = new(StringComparer.Ordinal)
    {
        ["RecuentosController.Confirmar"] =
            "numera, así que la clave es obligatoria por el motivo de s_obligatorias; y lo que " +
            "confirma es lo que se contó en un papel que varias personas escriben a la vez, así que " +
            "quien confirma tiene que citar la versión que vio (ADR-0055 §2). Ni la máquina de " +
            "estados ni el testigo bastan: un conteo de otra persona deja el recuento en curso y " +
            "con otra cifra, y confirmar sin haberla visto daría por buena una diferencia que nadie " +
            "ha mirado",

        ["RecuentosController.Anular"] =
            "numera, por el motivo de s_obligatorias; y quien anula tiene que citar la versión que " +
            "vio, porque anular deshace una diferencia confirmada y la ficha es la que la enseña. " +
            "Contesta a los dos motivos como la confirmación: sin ETag, y con el 412 antes de " +
            "escribir, con la cabecera bloqueada. El ajuste no choca contra su testigo porque solo " +
            "lo anula este camino, y siempre con esa cabecera bloqueada",

        ["RecuentosController.Descartar"] =
            "no numera, así que la clave se admite y no se exige, como en el alta; y descartar tira " +
            "lo que han contado otros, así que quien descarta tiene que citar la versión que vio: " +
            "un conteo que llega antes cambia lo que se pierde. Contesta a los dos motivos como la " +
            "confirmación, y sin clave la transacción es la de la unidad de trabajo",
    };

    // La clave que identifica una petición repetible lleva dentro la empresa y el usuario. Una
    // acción anónima no tiene ni lo uno ni lo otro, así que marcarla sería pedir una identidad que
    // no existe. Y no es una casualidad afortunada: las respuestas que llevan credenciales dentro
    // son precisamente las de los caminos anónimos —identificarse y renovar—, así que esta línea
    // es también la que impide, por construcción, que un token acabe guardado en la tabla.
    [Fact]
    public void Ninguna_accion_que_admite_idempotencia_es_anonima()
    {
        List<string> anonimas =
        [
            .. Todas()
                .Where(accion => accion.AdmiteIdempotencia && accion.EsAnonima)
                .Select(accion => accion.Nombre),
        ];

        anonimas.ShouldBeEmpty(
            "estas acciones admiten Idempotency-Key y son anónimas: " + string.Join(", ", anonimas));
    }

    // El filtro resuelve el almacén por el segmento de módulo de la ruta. Sin almacén registrado
    // para ese segmento, la primera petición con cabecera revienta con un 500 en ejecución. Aquí se
    // ve antes, y sin base de datos.
    [Fact]
    public void Cada_accion_que_admite_idempotencia_tiene_almacen_en_su_modulo()
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        List<string> huerfanas =
        [
            .. Todas()
                .Where(accion => accion.AdmiteIdempotencia)
                .Where(accion => alcance.ServiceProvider
                    .GetKeyedService<IAlmacenDeIdempotencia>(accion.Modulo) is null)
                .Select(accion => $"{accion.Nombre} (módulo {accion.Modulo})"),
        ];

        huerfanas.ShouldBeEmpty(
            "estas acciones admiten Idempotency-Key y su módulo no registra almacén: " +
            string.Join(", ", huerfanas));
    }

    // Los dos mecanismos a la vez solo los pide una acción que contesta a los dos motivos de
    // `s_conLosDos`, y la lista se compara entera en los dos sentidos. Una que falte es una
    // combinación colada sin haber decidido qué hace la repetición con su ETag ni dónde sale su
    // 412; una que sobre es un motivo escrito sobre una acción que ya no lo necesita.
    [Fact]
    public void Solo_piden_los_dos_mecanismos_las_acciones_que_dicen_por_que()
    {
        List<string> ambas =
        [
            .. Todas()
                .Where(accion => accion.AdmiteIdempotencia && accion.ExigeVersion)
                .Select(accion => accion.Nombre),
        ];

        ambas.ShouldNotBeEmpty("el contraejemplo: la confirmación del recuento pide los dos");

        List<string> sinMotivo = [.. ambas.Where(nombre => !s_conLosDos.ContainsKey(nombre))];

        sinMotivo.ShouldBeEmpty(
            "estas acciones exigen If-Match y admiten Idempotency-Key a la vez, y no dicen por qué: " +
            string.Join(", ", sinMotivo));

        List<string> sobran = [.. s_conLosDos.Keys.Where(nombre => !ambas.Contains(nombre))];

        sobran.ShouldBeEmpty(
            "estas acciones están declaradas con los dos mecanismos y ya no los piden (o ya no " +
            "existen): " + string.Join(", ", sobran));
    }

    // Un barrido que no encuentra nada sale verde por la peor de las razones: las cinco
    // comprobaciones de arriba recorren listas vacías y no comprueban nada. Que la reflexión deje de
    // encontrar acciones no es una hipótesis remota —basta con que un controlador cambie de espacio
    // de nombres, o que los verbos pasen a declararse de otra manera— y no tendría ningún otro
    // síntoma. Estos números son el inventario de hoy, y las tres tablas del ítem 0.9 en
    // `docs/PLAN.md` dicen los mismos. Cuando una fase añada acciones, este rojo obliga a mover el
    // número Y la tabla en el mismo cambio, que es justo lo que mantiene la documentación viva.
    [Fact]
    public void El_barrido_encuentra_el_inventario_entero()
    {
        List<Accion> todas = [.. Todas()];
        List<Accion> cambian = [.. todas.Where(accion => accion.CambiaEstado)];

        todas.Count.ShouldBe(149, "acciones en total");
        cambian.Count.ShouldBe(96, "acciones que cambian estado");

        // Los seis controladores del 0.15 suman veintisiete acciones, quince de ellas de escritura:
        // seis altas con clave de idempotencia, ocho modificaciones con If-Match —dos de impuestos,
        // porque cerrar un tramo va aparte— y el desbloqueo de ubicación, que se une a los otros
        // tres del cajón de las exentas por el argumento del ADR-0017.
        //
        // Trece y no dieciséis fue el número del 0.10: los tres `Desbloquear` dejaron de exigir
        // If-Match y se mudaron al cajón de las exentas sin mover el total, que es lo que dice que
        // fue una mudanza y no una acción nueva colada sin protección.
        //
        // Setenta y cuatro y no setenta y tres desde el ítem 1.3: `POST .../empresas/buscar` es
        // una acción nueva, y de las que cambian estado SEGÚN EL VERBO, no según lo que hace. Por
        // eso sube el total, sube el de escrituras y sube el cajón de las exentas, los tres a la
        // vez y en uno: una acción que subiera dos de los tres sería una que se protege o una que
        // se coló sin motivo escrito.
        //
        // Setenta y cinco desde el ítem 1.4, y esta vez sube UNO SOLO: `GET .../bloqueados` es el
        // acceso reservado del art. 32 (ADR-0027) y es una lectura, así que no cambia estado, no
        // exige If-Match, no admite Idempotency-Key y no está exenta de nada. Que suba solo el
        // total es la forma que tiene este recuento de decir que se ha añadido un camino de
        // LECTURA; el día que uno de los otros cuatro números se moviera con él, lo que se habría
        // colado sería una escritura.
        //
        // Ochenta y dos desde el ítem 1.5, y los cinco números se mueven a la vez porque lo que
        // entra es un MÓDULO entero, no una acción: Terceros publica siete rutas —dos lecturas,
        // el alta con clave, la modificación y el bloqueo con If-Match, y las dos exentas—. El
        // reparto, sumado, es la comprobación: +7 al total, +5 a las que cambian estado, +2 a
        // If-Match, +1 a Idempotency-Key y +2 al cajón de las exentas. Si las siete hubieran
        // entrado sin que subiera ninguno de los cuatro repartos, serían siete lecturas; si
        // subiera el total y no el reparto, sería una escritura sin protección ni motivo escrito.
        //
        // Noventa y tres desde el ítem 1.6, y el reparto dice de qué clase son las once nuevas:
        // +11 al total, +7 a las que cambian estado, +7 a If-Match, y CERO a Idempotency-Key y
        // cero al cajón de las exentas. Las cuatro que no cambian estado son las lecturas de lo
        // que cuelga —contactos, cuentas, condiciones y límite—. Que Idempotency-Key no se mueva
        // es la afirmación importante: colgar un contacto o una cuenta PARECE un alta, y si lo
        // fuera llevaría clave; no lo es, porque lo que se modifica es el agregado, que sí tiene
        // versión previa que citar. Los dos mecanismos a la vez solo los pide la lista cerrada del
        // test de arriba, así que la única manera de que ese número hubiera subido sería quitando el
        // If-Match — y entonces dos peticiones simultáneas sobre la misma ficha se pisarían.
        //
        // Ciento dos desde el ítem 1.7, y el reparto separa las nueve nuevas en dos clases. Ocho
        // son la retirada de los cuatro maestros de instalación —`POST` y `DELETE` de
        // `{id}/retirada` en divisas, cotizaciones, unidades y conversiones—: +8 al total, +8 a
        // las que cambian estado y +8 a If-Match, con Idempotency-Key y el cajón de las exentas
        // quietos. Que suban los tres a la vez es lo que dice que son escrituras protegidas y no
        // ocho puertas nuevas sin candado: retirar una divisa la quita de en medio en TODA la
        // instalación (R8), así que quien la retira tiene que estar citando la versión que vio.
        //
        // La novena es `GET .../conversiones-de-unidades/resolucion`, que sube el total y NADA
        // más, igual que hizo el listado de lo bloqueado en el 1.4: es una lectura. Y aquí el
        // reparto afirma algo del ADR-0023 que ninguna otra regla mira: resolver un par NO es una
        // escritura, no crea la conversión que falta ni la deduce encadenando dos factores; si
        // este número se hubiera movido con el otro, lo que se habría colado es exactamente eso.
        //
        // El `DELETE` de las ocho no contradice el «ningún DELETE, nunca» del ADR: lo prohibido es
        // `DELETE /{recurso}/{id}`, que borraría la fila, y de ese sigue sin haber ni uno para los
        // cuatro —lo vigila `NingunMaestroRetirableSeBorraTests`—. Estos ocho borran la retirada,
        // que es un sub-recurso: convive con el borrado del recurso sin ser lo mismo.
        //
        // Ciento diez desde el ítem 1.8, y otra vez entra un MÓDULO entero, como en el 1.5:
        // Catálogo publica ocho acciones —artículos y categorías, con las mismas cuatro cada uno—.
        // El reparto es +8 al total, +4 a las que cambian estado, +2 a If-Match, +2 a
        // Idempotency-Key y CERO al cajón de las exentas, y esos cinco números juntos dicen la
        // forma del módulo mejor que cualquier prosa: cuatro lecturas (dos listados y dos
        // consultas por id), dos altas con clave y dos modificaciones citando versión. Nada
        // exento, porque en Catálogo no hay ninguna escritura que no sea una de esas cuatro.
        //
        // Y no hay ningún `DELETE`, que aquí es la afirmación de fondo: un artículo no se borra ni
        // se retira ni se bloquea. Si el cajón de las exentas se hubiera movido con este ítem,
        // habría entrado una escritura sin candado y con motivo escrito a posteriori; si hubiera
        // subido If-Match sin subir Idempotency-Key, las dos altas se habrían colado exigiendo una
        // versión que un recurso que aún no existe no puede citar.
        //
        // Ciento veinte desde el ítem 1.9, y aquí no entra un módulo sino UN recurso con lo que le
        // cuelga: tarifas y sus líneas, diez acciones. El reparto es +10 al total, +5 a las que
        // cambian estado, +3 a If-Match, +2 a Idempotency-Key y CERO al cajón de las exentas.
        //
        // Las cinco lecturas son el listado de tramos, el tramo por id, el listado de líneas de un
        // tramo, la línea por id y —la quinta— la resolución de precio,
        // `GET /tarifas/{codigo}/precio`. Esa última es la que este reparto vigila de verdad:
        // resolver un precio es el camino caliente del módulo, se invoca una vez por línea de
        // documento, y NO cambia nada. El día que alguien la convirtiera en un `POST` porque «así
        // caben más parámetros», o le hiciera guardar la resolución, subirían dos números en vez de
        // uno y este rojo lo diría antes de que ninguna factura dependiera de ello.
        //
        // Las tres de If-Match son el nombre del tramo, el CIERRE del tramo y el precio de una
        // línea. Cerrar va aparte de modificar por lo mismo que en impuestos —cerrar un tramo no es
        // editarlo—, y las tres citan versión porque las tres reinterpretan precios que ya se
        // aplicaron: dos personas moviendo la misma tabla de precios a la vez es exactamente lo que
        // el 412 existe para parar.
        //
        // Las dos de Idempotency-Key son el alta del tramo y el alta de una línea. Que colgar una
        // línea SÍ lleve clave, cuando colgar un contacto en el 1.6 no la llevaba, no es una
        // incoherencia: allí lo que se modificaba era el agregado —el tercero, que ya tenía versión
        // que citar— y aquí la línea es una entidad con su propia identidad, su propio ETag y su
        // propio `PUT`. Un alta que no existía no puede citar la versión de nada, así que el
        // reintento de un móvil que perdió la cobertura solo lo para la clave.
        //
        // Y el cajón de las exentas quieto: en tarifas no hay ninguna escritura que no sea un alta
        // o una modificación. Tampoco hay ningún `DELETE` —una tarifa no se borra: se CIERRA, y el
        // tramo cerrado sigue diciendo a qué precio se vendió mientras regía—, que es la
        // afirmación que sostiene que reimprimir un albarán antiguo siga dando el mismo importe.
        //
        // Ciento veintisiete desde el ítem 1.10, y es la primera vez que el recuento sube por DOS
        // módulos a la vez, porque lo que entra es un cruce MUTUO: Catálogo aprende a colgarle
        // proveedores a un artículo y Terceros a asignarle una tarifa a una ficha, y cada uno
        // pregunta por el Contracts del otro. El reparto es +7 al total, +4 a las que cambian
        // estado, +3 a If-Match, +1 a Idempotency-Key y CERO al cajón de las exentas.
        //
        // Cinco son de Catálogo —dos lecturas, el alta con clave, la modificación de la referencia
        // y el `DELETE` del suministro, las dos últimas con If-Match— y dos de Terceros —la lectura
        // de la tarifa asignada y el `PUT` que la fija, con If-Match—. Que los dos módulos se
        // muevan en el mismo ítem es lo que dice que el cruce es de ida y vuelta: si solo hubiera
        // subido uno, lo declarado en `CrucesDeclarados` tendría una mitad sin código que la
        // ejerciera, y eso lo pone rojo el censo de fronteras, no este recuento.
        //
        // EL `DELETE` ES LA NOVEDAD QUE HAY QUE ARGUMENTAR, porque el reparto del 1.8 decía «no hay
        // ningún `DELETE`» de Catálogo como afirmación de fondo. Sigue sin haberlo de lo que aquel
        // párrafo protegía: no existe `DELETE /articulos/{id}` ni `DELETE /categorias/{id}`, y un
        // artículo se sigue sin poder borrar. Lo que se borra aquí es un suministro, que no es la
        // ficha de nadie sino un hecho entre dos que ha dejado de ser verdad —igual que el `DELETE`
        // de un contacto del 1.6 no borra al tercero—. Y lleva If-Match por eso mismo: quien lo
        // quita está diciendo que vio esa fila, no que quiere que desaparezca la que haya.
        //
        // LAS TRES LECTURAS SUBEN SOLO EL TOTAL, y una de ellas afirma algo que ninguna otra regla
        // mira: el listado de proveedores de un artículo FILTRA por el bloqueo del tercero (art. 32
        // de la LOPDGDD, R16) preguntándoselo a Terceros, y filtrar no es escribir. El día que a
        // alguien le pareciera caro preguntar y guardara aquí una copia de quién está bloqueado,
        // subirían dos números en vez de uno, y este rojo lo diría antes de que Catálogo tuviera su
        // propia lista de las bajas.
        //
        // Y EL ÚNICO +1 DE IDEMPOTENCY-KEY SEPARA LAS DOS ESCRITURAS QUE SE PARECEN. Colgar un
        // proveedor es un alta —una fila con su identidad, su ETag y su `PUT`, como la línea de
        // tarifa del 1.9— y un alta no puede citar la versión de lo que todavía no existe: solo la
        // clave para el reintento del móvil que perdió la cobertura. Fijar la tarifa asignada
        // PARECE lo mismo y no lo es: lo que se modifica es el tercero, que ya tiene versión que
        // citar, como los contactos del 1.6. Si este número hubiera subido dos, la segunda se
        // habría colado sin If-Match y dos personas cambiándole la tarifa al mismo cliente se
        // pisarían sin enterarse.
        //
        // Ciento veintiocho desde el ítem 1.11, y el reparto es el de un alta: +1 al total, +1 a las
        // que cambian estado, +1 a Idempotency-Key, cero a If-Match y cero a las exentas. Es
        // `POST .../terceros/importacion`, y lo que este reparto afirma es que la clave NO es opcional
        // en espíritu aunque lo sea en la cabecera: un fichero de cinco mil filas que se reenvía
        // porque el móvil perdió la cobertura son cinco mil `ya-existe` y un informe que ya no dice
        // qué entró. Que If-Match no se mueva dice lo otro: la importación no toca ninguna ficha que
        // exista (ADR-0034), así que no tiene versión que citar. Si el día que alguien la hiciera
        // actualizar subiera este número y no aquel, sería una escritura sobre fichas ajenas sin
        // candado.
        //
        // Y rompe una igualdad que estas tablas daban por hecha: hasta aquí las rutas con clave eran
        // las altas de un recurso con su `GET /{id}` y su `ETag`, dieciocho y dieciocho. Con esta son
        // diecinueve y dieciocho, y es correcto: la importación da de alta terceros, que ya tienen su
        // `GET /{id}`, pero ella no es un recurso ni tiene uno que devolver. Su respuesta es un
        // informe, no una ficha, y por eso responde `200` y no `201`.
        //
        // Ciento veintinueve desde el ítem 2.4, y es el primer número que mueve el módulo
        // Inventario: +1 al total, +1 a las que cambian estado, +1 a Idempotency-Key, cero a
        // If-Match y cero a las exentas. Es `POST .../inventario/ajustes/{id}/confirmacion`, y es
        // la ÚNICA acción que el módulo publica —ni alta, ni listado, ni ficha—: la superficie
        // del ajuste va con sus pantallas, y lo que este ítem necesitaba de la API era el sitio
        // donde el número entra en un recibo, que no existe sin una acción de MVC.
        //
        // QUE NO SUBA IF-MATCH ES LA AFIRMACIÓN, y no es que se le haya olvidado: confirmar dos
        // veces no lo para una versión, lo para la máquina de estados —el segundo intento se
        // encuentra un ajuste que ya no está en borrador—. Si este número hubiera subido,
        // además, habría chocado con la regla que entonces prohibía los dos mecanismos a la vez: un
        // documento en borrador NO tiene `GET` por el que sacar su `ETag`, porque el borde no
        // publica ninguno, así que la precondición no tendría llave —el mismo argumento del
        // ADR-0017 que mandó tres desbloqueos al cajón de las exentas en el 0.10—.
        //
        // Y aquí el reparto se queda CORTO por primera vez, que es lo que obliga al recuento nuevo
        // de abajo: esta clave no es opcional, es OBLIGATORIA, y el reparto de cinco números no
        // sabe distinguirlo. Para él la veinte es una más.
        // Ciento treinta desde el ítem 2.5, y el reparto vuelve a ser el mismo: +1 al total, +1 a
        // las que cambian estado, +1 a Idempotency-Key, cero a If-Match y cero a las exentas. Es
        // `POST .../inventario/ajustes/{id}/anulacion`, y no es superficie de más: el documento
        // que anula es otro ajuste confirmado, así que necesita número, el número necesita
        // transacción, y la transacción no existe sin una acción de MVC que dispare el filtro.
        //
        // QUE NO SUBA IF-MATCH VUELVE A SER LA AFIRMACIÓN, y por dos motivos que no son el mismo.
        // De anular dos veces SEGUIDAS protege la máquina de estados: el segundo intento se
        // encuentra un ajuste que ya no está confirmado. De anular dos veces A LA VEZ protege el
        // testigo de concurrencia de la fila (R11), que es un mecanismo del motor y no una
        // precondición del protocolo: el perdedor se lleva un 412 con la versión de ahora dentro
        // y su transacción entera se deshace, sin inverso y sin número gastado. Un `If-Match`
        // aquí habría pedido un `ETag` que el borde no publica, porque el ajuste sigue sin `GET`.
        //
        // Y el único número que sube de verdad de categoría es el de abajo: las obligatorias
        // pasan de una a DOS. Es el momento en que una excepción se convierte en costumbre si
        // nadie escribe el criterio, y el criterio no se ha ampliado para que quepa la segunda.
        //
        // Ciento treinta y cinco desde el ítem 2.10, y el reparto es el de la parte de Catálogo del
        // 1.10 con una lectura en el sitio de su modificación: +5 al total, +2 a las que cambian
        // estado, +1 a If-Match, +1 a Idempotency-Key y cero a las exentas. Son los códigos de
        // barras del artículo (ADR-0051): tres lecturas —los de un artículo, uno por id y la
        // búsqueda por GTIN—, el alta con clave y el `DELETE` con If-Match.
        //
        // QUE NO HAYA `PUT` ES LA AFIRMACIÓN. Un código de barras no se corrige: un GTIN dice una
        // presentación con sus unidades, y cambiarlas exige otro GTIN (ADR-0051 §3). Un nivel o
        // unas unidades mal puestos se arreglan quitándolo y volviéndolo a dar de alta. Si este
        // ítem hubiera subido dos a If-Match, habría entrado una modificación que cambia lo que un
        // lector de caja ya ha leído.
        //
        // Y LA BÚSQUEDA SUBE SOLO EL TOTAL, como la resolución de precio del 1.9: es el camino
        // caliente de quien escanea, y no guarda nada de lo que busca.
        //
        // Ciento treinta y ocho desde el ítem 2.11, y el reparto es tres veces el de la anulación
        // del 2.5: +3 al total, +3 a las que cambian estado, +3 a Idempotency-Key, cero a If-Match y
        // cero a las exentas. Son el envío, la recepción y la anulación de la transferencia
        // (ADR-0053 §9). Ninguna lectura: el alta, el listado y la ficha llegan con sus pantallas, y
        // que el total suba lo mismo que las escrituras es lo que lo dice.
        //
        // QUE NO SUBA IF-MATCH es la afirmación del 2.5 otra vez: la transferencia tampoco tiene un
        // `GET` que publique su `ETag`. De la carrera entre dos personas la para la versión de la
        // fila, que el caso de uso relee con las valoraciones ya bloqueadas, y sale por `412`.
        //
        // Ciento cuarenta y tres desde el ítem 2.12, y es la primera superficie de lectura del
        // módulo: +5 al total, +1 a las que cambian estado, +1 a Idempotency-Key, cero a If-Match y
        // cero a las exentas. Son las cuatro lecturas del recuento —la lista, la ficha, la página de
        // líneas y una línea, las dos con su `ETag`— y su alta (ADR-0055).
        //
        // EL ALTA ADMITE LA CLAVE Y NO LA EXIGE, como las de los maestros: no numera, así que no hay
        // hueco que evitar, y dos altas sobre el mismo almacén las para el índice de uno en curso.
        // Las escrituras que llegan después en el mismo ítem —contar, confirmar, anular, descartar—
        // mueven este número en su propio commit, con su reparto.
        //
        // Ciento cuarenta y seis con las tres escrituras en las líneas: +3 al total, +3 a las que
        // cambian estado, +2 a If-Match, +1 a Idempotency-Key y cero a las exentas. Contar y quitar
        // exigen la versión de la LÍNEA, que publica su `GET` (ADR-0055 §4); añadir admite la clave
        // y no la exige, como el alta, porque no numera y el reintento sin clave lo para la clave
        // repetida.
        //
        // QUE SUBA IF-MATCH Y NO LO HAGA EN EL AJUSTE NI EN LA TRANSFERENCIA es lo que separa a esta
        // pantalla de aquellos documentos: aquí dos personas escriben a la vez en el mismo papel, y
        // lo que una ve tiene que ser la versión de lo que la otra acaba de contar.
        //
        // Ciento cuarenta y siete con la confirmación, y es la primera acción que sube A LA VEZ
        // If-Match e Idempotency-Key: +1 al total, +1 a las que cambian estado, +1 a If-Match, +1 a
        // Idempotency-Key, +1 a las obligatorias y +1 a las que piden los dos (ADR-0057). Por eso
        // la partición de abajo resta las de los dos: sin esa resta, la confirmación caería en dos
        // cajones y la cuenta saldría una de más.
        //
        // Ciento cuarenta y nueve con la anulación y el descarte, y las dos piden los dos: +2 al
        // total, +2 a las que cambian estado, +2 a If-Match, +2 a Idempotency-Key, +2 a las que
        // piden los dos y +1 a las obligatorias. La anulación exige la clave, porque su inverso
        // numera; el descarte la admite y no la exige, porque no numera (ADR-0057, Consecuencias).
        cambian.Count(accion => accion.ExigeVersion).ShouldBe(52, "operaciones que exigen If-Match");
        cambian.Count(accion => accion.AdmiteIdempotencia)
            .ShouldBe(30, "rutas que admiten Idempotency-Key");
        s_exentas.Count.ShouldBe(17, "acciones exentas con motivo escrito");

        // Y de esas treinta, SIETE la exigen. Es un recuento aparte y no un reparto del anterior
        // porque las obligatorias son un SUBCONJUNTO de las que admiten, no un cuarto cajón: la
        // partición de abajo seguiría siendo exacta aunque las treinta fueran obligatorias, que es
        // justo lo que este número impide que pase sin que nadie lo vea. Las siete son del mismo
        // módulo. Seis por el argumento de la confirmación —número dentro de la transacción del
        // documento—, y la recepción de la transferencia, que no numera, porque sin la transacción
        // del filtro el cerrojo de la valoración no dura más que su sentencia, y el tránsito que
        // baja y la existencia que sube dejarían de ir juntos. Las siete están nombradas con su
        // motivo en `s_obligatorias`, que se compara entera en los dos sentidos: este número solo
        // dice cuántas, no cuáles.
        cambian.Count(accion => accion.ExigeIdempotencia)
            .ShouldBe(7, "rutas que EXIGEN Idempotency-Key");

        cambian.Count(accion => accion.ExigeVersion && accion.AdmiteIdempotencia)
            .ShouldBe(s_conLosDos.Count, "rutas que piden los dos mecanismos");

        // La partición es exacta: cada acción que cambia estado cae en uno de los tres cajones, y
        // solo las de `s_conLosDos` caen en dos. Los dos primeros tests lo comprueban por nombre;
        // esto lo comprueba por cuenta, que es lo que se rompe si alguien añade una acción y una
        // exención a la vez.
        (52 + 30 - s_conLosDos.Count + s_exentas.Count).ShouldBe(cambian.Count);
    }

    /// <summary>
    /// El universo del que salen las seis reglas de arriba no está vacío y cubre a todos los
    /// módulos que publican acciones.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es la afirmación que le faltaba a este fichero, y la que el ítem 1.2 enseñó a escribir. Los
    /// recuentos de <c>El_barrido_encuentra_el_inventario_entero</c> dicen cuántas acciones hay,
    /// pero no dicen de QUIÉN: con el universo escrito a mano, un módulo nuevo simplemente no
    /// entraba, sus acciones no se contaban, y los números seguían cuadrando porque tampoco había
    /// cambiado el número esperado. Verde por los dos lados a la vez.
    /// </para>
    /// <para>
    /// Aquí se comparan DOS fuentes que no se derivan una de otra: los módulos que el host enruta
    /// y los módulos que tienen controladores en el disco. La igualdad es legítima porque las dos
    /// describen el mismo conjunto —un módulo con controladores es un módulo que se monta— y por
    /// eso se comparan enteras y no por tamaño.
    /// </para>
    /// </remarks>
    [Fact]
    public void El_universo_cubre_a_todos_los_modulos_montados()
    {
        List<Accion> todas = [.. Todas()];

        todas.ShouldNotBeEmpty(
            "la tabla de enrutado no ha devuelto ni una acción: las seis reglas de este fichero " +
            "estarían recorriendo listas vacías y saldrían verdes sin comprobar nada");

        SortedSet<string> enrutados = new(
            todas.Select(accion => accion.Modulo), StringComparer.Ordinal);

        SortedSet<string> enElDisco = ModulosConControladoresEnElDisco();

        enElDisco.ShouldNotBeEmpty(
            "no se ha encontrado ni un ensamblado Bastion.<Modulo>.Endpoints con controladores " +
            "junto al binario de pruebas: sin segunda fuente, esta comparación no compara nada");

        enrutados.ShouldBe(
            enElDisco,
            customMessage:
            "los módulos que la API enruta no son los que tienen controladores en el disco. " +
            "Enrutados: " + string.Join(", ", enrutados) + ". En el disco: " +
            string.Join(", ", enElDisco) + ". Un módulo que solo está en el disco es un módulo " +
            "cuyas acciones nadie atiende ni vigila");
    }

    private IEnumerable<Accion> QueCambianEstado() =>
        Todas().Where(accion => accion.CambiaEstado);

    /// <summary>
    /// Todas las acciones que la API PUBLICA, leidas de su propia tabla de enrutado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El universo se descubre, no se escribe.</b> Hasta el item 1.3 esto era un array con un
    /// typeof por modulo montado, y ese array es el mismo modo de fallo que el item 1.2 encontro
    /// en otro sitio: el dia que Terceros estrene su controlador, las seis reglas de este fichero
    /// seguirian verdes sin haber mirado ni una de sus acciones, y no habria ningun rojo que lo
    /// dijera. Anadir un tercer typeof lo arreglaba hoy y lo rompia otra vez con Catalogo.
    /// </para>
    /// <para>
    /// La tabla de enrutado no tiene ese problema porque no es la lista de nadie: es lo que el
    /// host ha montado. Un modulo nuevo aparece aqui en cuanto se le anade su AgregarModuloDe en
    /// el arranque, que es exactamente el momento en que sus acciones empiezan a atender
    /// peticiones y por tanto el momento en que estas reglas tienen que empezar a mirarlas. Y es
    /// la MISMA fuente de la que sale el enrutado real, no una reconstruccion suya por reflexion.
    /// </para>
    /// </remarks>
    private IEnumerable<Accion> Todas()
    {
        IActionDescriptorCollectionProvider rutas =
            _api.Services.GetRequiredService<IActionDescriptorCollectionProvider>();

        foreach (ControllerActionDescriptor accion in rutas.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>())
        {
            MethodInfo metodo = accion.MethodInfo;
            TypeInfo controlador = accion.ControllerTypeInfo;

            yield return new Accion(
                $"{controlador.Name}.{metodo.Name}",
                ModuloDe(accion),
                accion.ActionConstraints?.OfType<HttpMethodActionConstraint>()
                    .SelectMany(limite => limite.HttpMethods).Any(EsDeEscritura) ?? false,
                metodo.GetParameters().Any(EsLaCabeceraIfMatch),
                metodo.GetCustomAttribute<AdmiteIdempotenciaAttribute>() is not null,
                metodo.GetCustomAttribute<AdmiteIdempotenciaAttribute>() is { Obligatoria: true },
                metodo.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                    || controlador.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
        }
    }

    /// <summary>Los modulos que este ensamblado de pruebas ve en el disco, con controladores.</summary>
    /// <remarks>
    /// La segunda fuente de El_universo_cubre_a_todos_los_modulos_montados, y es independiente de
    /// la primera: esta mira los ficheros que hay al lado del binario, aquella mira lo que el host
    /// enruta. Comparadas enteras, el rojo aparece por los dos lados: un modulo con controladores
    /// que nadie monto, y una ruta de un modulo que no esta en el disco.
    /// </remarks>
    private static SortedSet<string> ModulosConControladoresEnElDisco()
    {
        SortedSet<string> encontrados = new(StringComparer.Ordinal);

        foreach (string fichero in Directory.EnumerateFiles(
            AppContext.BaseDirectory, "Bastion.*.Endpoints.dll"))
        {
            string[] partes = Path.GetFileNameWithoutExtension(fichero).Split('.');

            if (partes.Length != 3)
            {
                continue;
            }

            bool lleva = Assembly.LoadFrom(fichero).GetTypes()
                .Any(tipo => tipo is { IsAbstract: false, IsPublic: true }
                    && typeof(ControllerBase).IsAssignableFrom(tipo));

            if (lleva)
            {
                encontrados.Add(partes[1].ToLowerInvariant());
            }
        }

        return encontrados;
    }

    private static bool EsDeEscritura(string verbo) =>
        verbo is "POST" or "PUT" or "PATCH" or "DELETE";

    private static bool EsLaCabeceraIfMatch(ParameterInfo parametro) =>
        string.Equals(
            parametro.GetCustomAttribute<FromHeaderAttribute>()?.Name,
            "If-Match",
            StringComparison.Ordinal);

    // El módulo sale de la ruta YA RESUELTA —`api/v1/{modulo}/{recurso}`—, que es de
    // donde lo saca también el filtro en ejecución. Leerlo del espacio de nombres daría el mismo
    // resultado hoy y dejaría de darlo el día que uno de los dos cambiara sin el otro.
    private static string ModuloDe(ControllerActionDescriptor accion)
    {
        string plantilla = accion.AttributeRouteInfo?.Template ?? string.Empty;

        string[] trozos = plantilla.Split('/', StringSplitOptions.RemoveEmptyEntries);

        trozos.Length.ShouldBeGreaterThanOrEqualTo(
            3,
            $"{accion.ControllerTypeInfo.Name}.{accion.MethodInfo.Name} no se enruta con la forma " +
            "api/v1/<modulo>/…");

        return trozos[2];
    }

    private sealed record Accion(
        string Nombre,
        string Modulo,
        bool CambiaEstado,
        bool ExigeVersion,
        bool AdmiteIdempotencia,
        bool ExigeIdempotencia,
        bool EsAnonima);
}
