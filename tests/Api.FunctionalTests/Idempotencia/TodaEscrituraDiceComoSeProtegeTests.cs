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

    // Hoy no hay ninguna que necesite los dos, y la combinación no está probada. Dos motivos, y el
    // segundo es el duro:
    //
    // 1. La repetición devolvería una respuesta con el ETag de entonces, que ya no sería el actual.
    // 2. La transacción de la idempotencia va SIN puntos de guardado (ver AlmacenDeIdempotencia), y
    //    un choque de concurrencia la dejaría abortada: el manejador del 412 consulta la versión
    //    actual de la fila para ponerla en la respuesta, y esa consulta fallaría. El cliente
    //    recibiría un 500 donde tocaba un 412 — y lo reintentaría, que es lo contrario de lo que
    //    hay que hacer con un choque.
    //
    // El día que haga falta, este rojo obliga a decidir las dos cosas antes de escribirlo.
    [Fact]
    public void Ninguna_accion_pide_los_dos_mecanismos_a_la_vez()
    {
        List<string> ambas =
        [
            .. Todas()
                .Where(accion => accion.AdmiteIdempotencia && accion.ExigeVersion)
                .Select(accion => accion.Nombre),
        ];

        ambas.ShouldBeEmpty(
            "estas acciones exigen If-Match y admiten Idempotency-Key a la vez: " +
            string.Join(", ", ambas));
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

        todas.Count.ShouldBe(75, "acciones en total");
        cambian.Count.ShouldBe(48, "acciones que cambian estado");

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
        cambian.Count(accion => accion.ExigeVersion).ShouldBe(21, "operaciones que exigen If-Match");
        cambian.Count(accion => accion.AdmiteIdempotencia)
            .ShouldBe(12, "rutas que admiten Idempotency-Key");
        s_exentas.Count.ShouldBe(15, "acciones exentas con motivo escrito");

        // La partición es exacta: cada acción que cambia estado cae en uno de los tres cajones y en
        // ninguno cae dos veces. Los dos primeros tests lo comprueban por nombre; esto lo comprueba
        // por cuenta, que es lo que se rompe si alguien añade una acción y una exención a la vez.
        (21 + 12 + s_exentas.Count).ShouldBe(cambian.Count);
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
        bool EsAnonima);
}
