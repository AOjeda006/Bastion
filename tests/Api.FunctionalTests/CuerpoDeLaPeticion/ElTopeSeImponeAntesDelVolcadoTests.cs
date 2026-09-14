using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Idempotencia;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.CuerpoDeLaPeticion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bastion.Api.FunctionalTests.CuerpoDeLaPeticion;

/// <summary>
/// El tope del cuerpo se impone MIENTRAS se lee, y se comprueba contando los bytes que se piden a la
/// corriente, no mirando la respuesta.
/// </summary>
/// <remarks>
/// <para>
/// <b>La respuesta no distingue un tope de verdad de uno falso.</b> Un lector que copiara el cuerpo
/// entero a memoria y comprobara el tamaño después contestaría el mismo <c>413</c>, con el mismo
/// <c>type</c>, y habría gastado la memoria que el tope existe para no gastar (ADR-0034 §3). Lo único
/// que los separa es cuánto se ha leído, así que eso es lo que se mide: la corriente de estos casos
/// cuenta cada byte que entrega.
/// </para>
/// <para>
/// <b>Y el filtro de idempotencia tiene su propio caso</b>, ejecutado solo, sin el filtro del tope
/// delante. Es el otro sitio que vuelca el cuerpo, y el que lo volcaba entero hasta el 1.11: si un día
/// dejara de usar el lector acotado, el filtro del tope seguiría tapándolo mientras corriera antes, y
/// el hueco solo se abriría el día que alguien cambiara el orden.
/// </para>
/// </remarks>
public sealed class ElTopeSeImponeAntesDelVolcadoTests
{
    private const int Tope = 1024;

    // Mucho más que el tope y más que un trozo de lectura: un lector que volcara entero lo pediría
    // todo, y uno acotado se para en tope + 1.
    private const int Enorme = 256 * 1024;

    [Fact]
    public async Task Con_un_Content_Length_mayor_que_el_tope_se_contesta_sin_leer_un_byte()
    {
        CorrienteQueCuenta cuerpo = new(Enorme);
        DefaultHttpContext contexto = Peticion(cuerpo, contentLength: Tope + 1);

        Resultado<ReadOnlyMemory<byte>> leido = await LectorAcotadoDelCuerpo.LeerAsync(contexto, Tope, CancellationToken.None);

        leido.EsCorrecto.ShouldBeFalse();
        leido.Error!.Codigo.ShouldBe(ErroresDelCuerpo.CodigoDeDemasiadoGrande);
        leido.Error.Tipo.ShouldBe(TipoDeError.DemasiadoGrande);
        cuerpo.Entregados.ShouldBe(0, "la petición declaraba más que el tope y aun así se ha leído");
    }

    [Fact]
    public async Task Sin_Content_Length_se_leen_como_mucho_tope_mas_uno_bytes()
    {
        CorrienteQueCuenta cuerpo = new(Enorme);
        DefaultHttpContext contexto = Peticion(cuerpo, contentLength: null);

        Resultado<ReadOnlyMemory<byte>> leido = await LectorAcotadoDelCuerpo.LeerAsync(contexto, Tope, CancellationToken.None);

        leido.EsCorrecto.ShouldBeFalse();
        leido.Error!.Codigo.ShouldBe(ErroresDelCuerpo.CodigoDeDemasiadoGrande);
        cuerpo.Entregados.ShouldBe(
            Tope + 1,
            "el lector ha pedido a la red más de lo que hacía falta para saber que el cuerpo no cabía: " +
            "un tope comprobado después del volcado no es un tope");
    }

    [Fact]
    public async Task Un_cuerpo_del_tamano_del_tope_entra_entero_y_no_se_vuelve_a_pedir_a_la_red()
    {
        CorrienteQueCuenta cuerpo = new(Tope);
        DefaultHttpContext contexto = Peticion(cuerpo, contentLength: null);

        Resultado<ReadOnlyMemory<byte>> primera = await LectorAcotadoDelCuerpo.LeerAsync(contexto, Tope, CancellationToken.None);
        Resultado<ReadOnlyMemory<byte>> segunda = await LectorAcotadoDelCuerpo.LeerAsync(contexto, Tope, CancellationToken.None);

        primera.EsCorrecto.ShouldBeTrue();
        primera.Valor.Length.ShouldBe(Tope);
        segunda.Valor.ToArray().ShouldBe(primera.Valor.ToArray());
        cuerpo.Entregados.ShouldBe(Tope, "la segunda lectura ha vuelto a la red en vez de usar lo leído");

        // Y quien lea la corriente después —el enlace del modelo de una acción JSON— encuentra los
        // mismos bytes, no una corriente ya agotada.
        using var copia = new MemoryStream();
        await contexto.Request.Body.CopyToAsync(copia);
        copia.ToArray().ShouldBe(primera.Valor.ToArray());
        LectorAcotadoDelCuerpo.Leido(contexto)!.Value.Length.ShouldBe(Tope);
    }

    [Fact]
    public async Task El_filtro_de_idempotencia_lee_con_el_tope_aunque_corra_sin_el_filtro_del_tope_delante()
    {
        CorrienteQueCuenta cuerpo = new(Enorme);
        DefaultHttpContext http = Peticion(cuerpo, contentLength: null);
        http.Request.Headers[FiltroDeIdempotencia.Cabecera] = "una-clave-cualquiera";

        AlmacenQueApunta almacen = new();
        ServiceCollection servicios = new();
        servicios.AddKeyedSingleton<IAlmacenDeIdempotencia>("terceros", almacen);
        servicios.AddProblemDetails();
        servicios.AddLogging();
        using ServiceProvider proveedor = servicios.BuildServiceProvider();
        http.RequestServices = proveedor;

        FiltroDeIdempotencia filtro = new(
            proveedor, new UsuarioConEmpresa(), new InquilinoConEmpresa(), TimeProvider.System,
            NullLogger<FiltroDeIdempotencia>.Instance);

        ActionContext accion = new(http, new RouteData(), new ActionDescriptor
        {
            EndpointMetadata = [new AdmiteIdempotenciaAttribute(), new TopeDelCuerpoAttribute(Tope)],
        });
        ResourceExecutingContext contexto = new(accion, [], []);
        bool trabajo = false;

        await filtro.OnResourceExecutionAsync(contexto, () =>
        {
            trabajo = true;
            return Task.FromResult(new ResourceExecutedContext(accion, []));
        });

        contexto.Result.ShouldNotBeNull();
        await contexto.Result.ExecuteResultAsync(accion);
        http.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);

        cuerpo.Entregados.ShouldBe(Tope + 1, "el filtro de idempotencia ha volcado el cuerpo sin tope para calcular la huella");
        trabajo.ShouldBeFalse();
        almacen.AbrioTransaccion.ShouldBeFalse("un cuerpo que no se ha podido leer no puede reclamar ninguna clave");
    }

    private static DefaultHttpContext Peticion(Stream cuerpo, long? contentLength)
    {
        DefaultHttpContext contexto = new();
        contexto.Request.Method = HttpMethods.Post;
        contexto.Request.Path = "/api/v1/terceros/terceros/importacion";
        contexto.Request.Body = cuerpo;
        contexto.Request.ContentLength = contentLength;
        contexto.Response.Body = new MemoryStream();
        return contexto;
    }

    // Una corriente que entrega tantos bytes como se le diga, y cuenta cuántos ha entregado.
    private sealed class CorrienteQueCuenta(long total) : Stream
    {
        public long Entregados { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Entregar(buffer.AsSpan(offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Entregar(buffer.Span));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromResult(Entregar(buffer.AsSpan(offset, count)));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Entregar(Span<byte> destino)
        {
            int cuantos = (int)Math.Min(destino.Length, total - Entregados);
            destino[..cuantos].Fill((byte)'x');
            Entregados += cuantos;
            return cuantos;
        }
    }

    private sealed class AlmacenQueApunta : IAlmacenDeIdempotencia
    {
        public bool AbrioTransaccion { get; private set; }

        public Task AbrirTransaccionAsync(CancellationToken cancelacion)
        {
            AbrioTransaccion = true;
            return Task.CompletedTask;
        }

        public Task ConfirmarAsync(CancellationToken cancelacion) => Task.CompletedTask;

        public Task DeshacerAsync(CancellationToken cancelacion) => Task.CompletedTask;

        public Task<bool> ReclamarAsync(
            ClaveDeIdempotencia clave, string huella, DateTimeOffset ahora, CancellationToken cancelacion) =>
            Task.FromResult(true);

        public Task<RegistroDeIdempotencia?> BuscarAsync(ClaveDeIdempotencia clave, CancellationToken cancelacion) =>
            Task.FromResult<RegistroDeIdempotencia?>(null);

        public Task GuardarRespuestaAsync(RespuestaGuardada respuesta, CancellationToken cancelacion) =>
            Task.CompletedTask;
    }

    private sealed class UsuarioConEmpresa : IUsuarioActual
    {
        public bool EstaAutenticado => true;

        public Guid UsuarioId { get; } = Guid.CreateVersion7();

        public Guid EmpresaId { get; } = Guid.CreateVersion7();

        public bool Tiene(Permiso permiso) => false;
    }

    private sealed class InquilinoConEmpresa : IInquilinoActual
    {
        public Guid? EmpresaDelFiltro => Guid.CreateVersion7();

        public bool HayEmpresaActiva => true;

        public MotivoSinInquilino? MotivoDelAmbito => null;

        public IDisposable SinInquilino(MotivoSinInquilino motivo) => throw new NotSupportedException();
    }
}
