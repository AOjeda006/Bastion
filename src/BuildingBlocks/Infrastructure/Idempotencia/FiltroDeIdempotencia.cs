using System.Text;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Idempotencia;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Bastion.BuildingBlocks.Infrastructure.Idempotencia;

/// <summary>
/// Hace cumplir la <c>Idempotency-Key</c> (R10): reclama la clave, deja pasar el trabajo, guarda la
/// respuesta y confirma; o devuelve la que se guardó la primera vez.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es un filtro de RECURSO y no de acción, y la diferencia importa dos veces.</b> Uno de recurso
/// corre <b>antes del enlace del modelo</b> —así puede leer los bytes del cuerpo tal como llegaron,
/// que es sobre lo que se calcula la huella— y <b>envuelve también la ejecución del resultado</b>
/// —así puede quedarse con los bytes de la respuesta ya serializada, que es lo que hay que guardar
/// para repetirla igual—. Uno de acción no llega ni a lo uno ni a lo otro: vería el DTO ya
/// deserializado y un <c>IActionResult</c> sin ejecutar.
/// </para>
/// <para>
/// <b>El filtro es el dueño de la transacción, y por eso el recibo y el trabajo son atómicos.</b>
/// Abre la transacción sobre el contexto del módulo antes de reclamar, y la confirma después de
/// anotar la respuesta. Entre medias, el <c>SaveChanges</c> del caso de uso —con su traza (0.7) y
/// sus eventos (0.8) dentro— cae en esa misma transacción sin enterarse. Consecuencia comprobable:
/// la fila de negocio y la del recibo llevan el <b>mismo <c>xmin</c></b>, que es el identificador de
/// la transacción que las escribió.
/// </para>
/// <para>
/// <b>Solo se guardan las respuestas de éxito.</b> Si la acción falla, se deshace todo —incluida la
/// reclamación— y la clave queda libre. Guardar el fallo la dejaría clavada: el mismo reintento que
/// habría funcionado devolvería para siempre el error de la primera vez. Así, la invariante de la
/// tabla se lee de una vez: <b>la clave existe si y solo si el trabajo ocurrió</b>.
/// </para>
/// </remarks>
/// <param name="proveedor">Para resolver el almacén del módulo al que va la petición.</param>
/// <param name="usuario">Quién pide, y en qué empresa (R8).</param>
/// <param name="inquilino">Para poder preguntar si hay empresa activa sin provocar la excepción.</param>
/// <param name="reloj">De dónde sale el instante de la reclamación.</param>
/// <param name="registro">Registro estructurado.</param>
public sealed partial class FiltroDeIdempotencia(
    IServiceProvider proveedor,
    IUsuarioActual usuario,
    IInquilinoActual inquilino,
    TimeProvider reloj,
    ILogger<FiltroDeIdempotencia> registro) : IAsyncResourceFilter
{
    /// <summary>La cabecera, con la grafía del borrador del IETF.</summary>
    public const string Cabecera = "Idempotency-Key";

    /// <inheritdoc />
    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        HttpRequest peticion = context.HttpContext.Request;
        StringValues cabecera = peticion.Headers[Cabecera];

        // Sin cabecera no hay nada que hacer: la operación sigue su camino de siempre. La clave es
        // una garantía que el cliente PIDE, no un peaje que se le cobra.
        //
        // «Sin cabecera» es que NO VENGA, y se pregunta por el número de valores y no por si el
        // texto está en blanco. Una cabecera presente y vacía es un cliente que cree que se está
        // protegiendo: se le contesta 400 unas líneas más abajo, en vez de atenderle sin
        // protección y dejar que lo descubra el día que reintente y duplique.
        if (cabecera.Count == 0)
        {
            await next().ConfigureAwait(false);
            return;
        }

        string metodo = peticion.Method;
        string ruta = peticion.Path.Value ?? string.Empty;

        if (!Admite(context))
        {
            context.Result = ErroresDeIdempotencia.NoAdmitida(metodo, ruta).AResultadoDeAccion();
            return;
        }

        if (!usuario.EstaAutenticado || !inquilino.HayEmpresaActiva)
        {
            context.Result = ErroresDeIdempotencia.SinEmpresaActiva().AResultadoDeAccion();
            return;
        }

        Resultado<ClaveDeIdempotencia> clave = ClaveDeIdempotencia.De(
            usuario.EmpresaId, usuario.UsuarioId, metodo, ruta, cabecera);

        if (!clave.EsCorrecto)
        {
            context.Result = clave.Error!.AResultadoDeAccion();
            return;
        }

        await AplicarAsync(context, next, clave.Valor).ConfigureAwait(false);
    }

    private static bool Admite(ResourceExecutingContext context) =>
        context.ActionDescriptor.EndpointMetadata.OfType<AdmiteIdempotenciaAttribute>().Any();

    // El módulo sale del tercer segmento de la ruta: /api/v1/<modulo>/<recurso>. Es la misma
    // convención que fija el Anexo A.1 y la que ya usa la ruta base de cada controlador, así que
    // no hay una segunda lista de módulos que mantener al día.
    private static string? ModuloDe(string ruta)
    {
        string[] trozos = ruta.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return trozos.Length >= 3 && string.Equals(trozos[0], "api", StringComparison.Ordinal)
            ? trozos[2]
            : null;
    }

    private static async Task<string> HuellaDelCuerpoAsync(HttpRequest peticion, CancellationToken cancelacion)
    {
        // El cuerpo se lee ENTERO y se rebobina: detrás viene el enlace del modelo, que lo
        // necesita intacto. Sin `EnableBuffering` la corriente solo se puede leer una vez y la
        // acción recibiría un cuerpo vacío.
        peticion.EnableBuffering();

        using var copia = new MemoryStream();
        await peticion.Body.CopyToAsync(copia, cancelacion).ConfigureAwait(false);
        peticion.Body.Position = 0;

        return ClaveDeIdempotencia.HuellaDe(copia.GetBuffer().AsSpan(0, (int)copia.Length));
    }

    private async Task AplicarAsync(
        ResourceExecutingContext context, ResourceExecutionDelegate next, ClaveDeIdempotencia clave)
    {
        CancellationToken cancelacion = context.HttpContext.RequestAborted;
        IAlmacenDeIdempotencia almacen = AlmacenDe(context.HttpContext.Request.Path.Value ?? string.Empty);
        string huella = await HuellaDelCuerpoAsync(context.HttpContext.Request, cancelacion).ConfigureAwait(false);

        await almacen.AbrirTransaccionAsync(cancelacion).ConfigureAwait(false);

        bool confirmada = false;

        try
        {
            bool mia = await almacen
                .ReclamarAsync(clave, huella, reloj.GetUtcNow(), cancelacion)
                .ConfigureAwait(false);

            if (!mia)
            {
                context.Result = await YaAtendidaAsync(almacen, clave, huella, cancelacion)
                    .ConfigureAwait(false);
                return;
            }

            confirmada = await TrabajarYGuardarAsync(context, next, almacen, cancelacion)
                .ConfigureAwait(false);
        }
        finally
        {
            if (!confirmada)
            {
                await almacen.DeshacerAsync(cancelacion).ConfigureAwait(false);
            }
        }
    }

    // La clave ya estaba tomada: o se repite lo que se respondió, o se dice que no es la misma
    // petición. Nunca se hace el trabajo otra vez.
    private async Task<IActionResult> YaAtendidaAsync(
        IAlmacenDeIdempotencia almacen,
        ClaveDeIdempotencia clave,
        string huella,
        CancellationToken cancelacion)
    {
        // No hay camino que lleve aquí: la reclamación solo falla porque la fila existe y está
        // confirmada. Se lanza en vez de improvisar un desenlace, porque cualquiera de los dos
        // que se eligiera —repetir o rechazar— sería mentira sobre algo que no se ha podido ver.
        RegistroDeIdempotencia? previa = await almacen.BuscarAsync(clave, cancelacion).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "La clave de idempotencia está tomada y su fila no se ve desde esta petición. " +
            "Es señal de que alguien escribe en la tabla fuera del filtro, o de que el filtro " +
            "de empresa no está alcanzando a esa fila.");

        if (!previa.CoincideElCuerpo(huella))
        {
            RegistrarCuerpoDistinto(registro, clave.Metodo, clave.Ruta, clave.EmpresaId, clave.UsuarioId);

            return ErroresDeIdempotencia.CuerpoDistinto().AResultadoDeAccion();
        }

        RespuestaGuardada guardada = previa.Respuesta;

        RegistrarRepeticion(
            registro, clave.Metodo, clave.Ruta, clave.EmpresaId, clave.UsuarioId, guardada.CodigoDeEstado);

        return new RespuestaRepetida(guardada);
    }

    // El trabajo, con la respuesta capturada. Devuelve si la transacción quedó confirmada.
    private static async Task<bool> TrabajarYGuardarAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next,
        IAlmacenDeIdempotencia almacen,
        CancellationToken cancelacion)
    {
        HttpResponse respuesta = context.HttpContext.Response;
        Stream original = respuesta.Body;

        using var capturada = new MemoryStream();
        respuesta.Body = capturada;

        ResourceExecutedContext ejecutado;

        try
        {
            ejecutado = await next().ConfigureAwait(false);
        }
        finally
        {
            // Se devuelve la corriente de verdad SIEMPRE, también si el trabajo estalló: lo que
            // venga detrás —el manejador central de excepciones— escribe su ProblemDetails ahí, y
            // con la corriente aún desviada su respuesta se quedaría en un `MemoryStream` que nadie
            // lee. Cero bytes de vuelta y una petición colgada.
            respuesta.Body = original;
        }

        // Una excepción sin tratar no deja respuesta que guardar, y lo que haya en el búfer es un
        // resultado a medio escribir: se tira. El manejador central escribirá el suyo entero.
        if (ejecutado.Exception is not null && !ejecutado.ExceptionHandled)
        {
            return false;
        }

        bool exito = respuesta.StatusCode is >= 200 and < 300;

        if (exito)
        {
            await almacen.GuardarRespuestaAsync(
                Recoger(respuesta, capturada), cancelacion).ConfigureAwait(false);

            await almacen.ConfirmarAsync(cancelacion).ConfigureAwait(false);
        }
        else
        {
            // El fallo se devuelve tal cual, pero no se apunta: la clave tiene que quedar libre
            // para que el mismo reintento pueda salir bien.
            await almacen.DeshacerAsync(cancelacion).ConfigureAwait(false);
        }

        capturada.Position = 0;
        await capturada.CopyToAsync(original, cancelacion).ConfigureAwait(false);

        return exito;
    }

    private static RespuestaGuardada Recoger(HttpResponse respuesta, MemoryStream capturada)
    {
        string cuerpo = Encoding.UTF8.GetString(
            capturada.GetBuffer().AsSpan(0, (int)capturada.Length));

        return new RespuestaGuardada(
            respuesta.StatusCode,
            cuerpo.Length == 0 ? null : cuerpo,
            respuesta.ContentType,
            respuesta.Headers.ETag.ToString() is { Length: > 0 } etiqueta ? etiqueta : null,
            respuesta.Headers.Location.ToString() is { Length: > 0 } ubicacion ? ubicacion : null);
    }

    private IAlmacenDeIdempotencia AlmacenDe(string ruta)
    {
        string? modulo = ModuloDe(ruta);

        IAlmacenDeIdempotencia? almacen = modulo is null
            ? null
            : proveedor.GetKeyedService<IAlmacenDeIdempotencia>(modulo);

        return almacen ?? throw new InvalidOperationException(
            $"No hay almacén de idempotencia registrado para la ruta {ruta}. Una acción marcada " +
            "con [AdmiteIdempotencia] necesita que su módulo registre el suyo: sin él, la clave " +
            "y el trabajo no podrían caer en la misma transacción.");
    }

    // La CLAVE del cliente no entra en el registro, y el resto de la tupla sí. La elige quien
    // llama, y hay quien mete dentro el número de pedido o el correo de alguien: en un registro
    // que se conserva, eso es un dato de un tercero que nadie decidió guardar ahí. Con el método,
    // la ruta, la empresa y el usuario se sigue el rastro igual de bien.
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Repetición idempotente de {Metodo} {Ruta} (empresa {EmpresaId}, usuario " +
            "{UsuarioId}): se devuelve la respuesta {Estado} guardada.")]
    private static partial void RegistrarRepeticion(
        ILogger registro, string metodo, string ruta, Guid empresaId, Guid usuarioId, int estado);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Idempotency-Key reutilizada con otro cuerpo en {Metodo} {Ruta} (empresa " +
            "{EmpresaId}, usuario {UsuarioId}): se responde 409.")]
    private static partial void RegistrarCuerpoDistinto(
        ILogger registro, string metodo, string ruta, Guid empresaId, Guid usuarioId);
}
