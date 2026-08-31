using System.Reflection;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Identidad.Endpoints.Comun;
using Bastion.Organizacion.Endpoints.Comun;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
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
        ["EmpresasController.Desbloquear"] =
            "no puede exigir If-Match desde el 0.10: la etiqueta se obtiene leyendo el recurso, y " +
            "una empresa bloqueada contesta 404 a su propio GET. Una precondición cuya llave no " +
            "hay manera de conseguir no es una precondición, es un muro. Y no hace falta: " +
            "mientras está bloqueada ninguna otra escritura llega a la fila —todas la piden al " +
            "repositorio y el filtro no se la da—, así que no hay con quién competir; desbloquear " +
            "dos veces deja el mismo estado (ADR-0017)",

        ["AlmacenesController.Desbloquear"] =
            "lo mismo, y con la misma consecuencia: el almacén bloqueado no emite ETag porque no " +
            "se deja leer. El testigo de concurrencia SIGUE comprobándose dentro de la petición " +
            "—se lee la fila y se guarda en la misma transacción—; lo que desaparece es la " +
            "precondición que el cliente cita, no la protección (ADR-0017)",

        ["UsuariosController.Desbloquear"] =
            "igual que las dos de Organización. Y aquí conviene dejar escrito lo que NO es: esto " +
            "levanta el bloqueo del art. 32, no el rechazo temporal por intentos fallidos, que " +
            "vive en `rechazado_hasta`, se levanta solo y nunca sacó al usuario de las consultas " +
            "(ADR-0017)",
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

        todas.Count.ShouldBe(46, "acciones en total");
        cambian.Count.ShouldBe(32, "acciones que cambian estado");

        // Trece y no dieciséis desde el 0.10: los tres `Desbloquear` dejaron de exigir If-Match, y
        // los tres se han mudado al cajón de las exentas, que pasa de diez a trece. El total de
        // acciones que cambian estado no se mueve, y eso es lo que dice que fue una mudanza y no
        // una acción nueva que se ha colado sin protección.
        cambian.Count(accion => accion.ExigeVersion).ShouldBe(13, "operaciones que exigen If-Match");
        cambian.Count(accion => accion.AdmiteIdempotencia)
            .ShouldBe(6, "rutas que admiten Idempotency-Key");
        s_exentas.Count.ShouldBe(13, "acciones exentas con motivo escrito");

        // La partición es exacta: cada acción que cambia estado cae en uno de los tres cajones y en
        // ninguno cae dos veces. Los dos primeros tests lo comprueban por nombre; esto lo comprueba
        // por cuenta, que es lo que se rompe si alguien añade una acción y una exención a la vez.
        (13 + 6 + s_exentas.Count).ShouldBe(cambian.Count);
    }

    private static IEnumerable<Accion> QueCambianEstado() =>
        Todas().Where(accion => accion.CambiaEstado);

    private static IEnumerable<Accion> Todas()
    {
        Assembly[] ensamblados =
        [
            typeof(ControladorDeOrganizacion).Assembly,
            typeof(ControladorDeIdentidad).Assembly,
        ];

        foreach (Type controlador in ensamblados
            .SelectMany(ensamblado => ensamblado.GetTypes())
            .Where(tipo => tipo is { IsAbstract: false, IsPublic: true }
                && typeof(ControllerBase).IsAssignableFrom(tipo)))
        {
            string modulo = ModuloDe(controlador);

            foreach (MethodInfo metodo in controlador.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                HttpMethodAttribute[] verbos = [.. metodo.GetCustomAttributes<HttpMethodAttribute>()];

                if (verbos.Length == 0)
                {
                    continue;
                }

                yield return new Accion(
                    $"{controlador.Name}.{metodo.Name}",
                    modulo,
                    verbos.SelectMany(verbo => verbo.HttpMethods).Any(EsDeEscritura),
                    metodo.GetParameters().Any(EsLaCabeceraIfMatch),
                    metodo.GetCustomAttribute<AdmiteIdempotenciaAttribute>() is not null,
                    metodo.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                        || controlador.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
            }
        }
    }

    private static bool EsDeEscritura(string verbo) =>
        verbo is "POST" or "PUT" or "PATCH" or "DELETE";

    private static bool EsLaCabeceraIfMatch(ParameterInfo parametro) =>
        string.Equals(
            parametro.GetCustomAttribute<FromHeaderAttribute>()?.Name,
            "If-Match",
            StringComparison.Ordinal);

    // El módulo sale de la ruta base del controlador —`api/v1/{modulo}/[controller]`—, que es de
    // donde lo saca también el filtro en ejecución. Leerlo del espacio de nombres daría el mismo
    // resultado hoy y dejaría de darlo el día que uno de los dos cambiara sin el otro.
    private static string ModuloDe(Type controlador)
    {
        string plantilla = controlador.GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(ruta => ruta.Template)
            .FirstOrDefault() ?? string.Empty;

        string[] trozos = plantilla.Split('/', StringSplitOptions.RemoveEmptyEntries);

        trozos.Length.ShouldBeGreaterThanOrEqualTo(
            3, $"{controlador.Name} no tiene una ruta base con la forma api/v1/<modulo>/…");

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
