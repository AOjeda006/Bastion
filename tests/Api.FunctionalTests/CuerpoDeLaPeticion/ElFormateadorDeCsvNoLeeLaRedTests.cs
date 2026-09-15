using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.CuerpoDeLaPeticion;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shouldly;

namespace Bastion.Api.FunctionalTests.CuerpoDeLaPeticion;

/// <summary>
/// El formateador de CSV entrega lo que ya leyó el lector acotado, y nunca va a la red por su cuenta.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las dos promesas de <see cref="FormateadorDeCsv"/> que ninguna petición de verdad pone a prueba.</b>
/// Todas las acciones que reciben un CSV hoy declaran su tope, así que el camino de «sin tope, lanza» no
/// lo recorre nada; y quitarlo —leer la corriente si no hay nada leído— no pondría rojo ningún caso de la
/// importación, que seguiría funcionando con el cuerpo volcado sin tope.
/// </para>
/// <para>
/// <b>El fichero vacío</b> lo encontró el sondeo de ficheros hostiles del ítem 1.11: la base del
/// formateador contesta «sin valor» a un cuerpo de cero bytes, y el enlace lo convertía en un
/// <c>datos-no-validos</c> con el texto en inglés del marco. Aquí se fija sin levantar el host.
/// </para>
/// </remarks>
public sealed class ElFormateadorDeCsvNoLeeLaRedTests
{
    private const int Tope = 1024;

    [Fact]
    public async Task Sin_lo_leido_por_el_tope_lanza_y_no_toca_la_corriente()
    {
        CorrienteQueNoSeLee cuerpo = new();
        DefaultHttpContext http = Peticion(cuerpo, contentLength: 3);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(
            () => new FormateadorDeCsv().ReadAsync(Contexto(http)));

        error.Message.ShouldContain("TopeDelCuerpo");
        cuerpo.Lecturas.ShouldBe(0, "el formateador ha ido a la red a leer un cuerpo que nadie acotó");
    }

    [Fact]
    public async Task Un_cuerpo_de_cero_bytes_llega_como_un_fichero_vacio_y_no_como_sin_valor()
    {
        DefaultHttpContext http = Peticion(new MemoryStream(), contentLength: 0);
        Resultado<ReadOnlyMemory<byte>> leido = await LectorAcotadoDelCuerpo.LeerAsync(http, Tope, CancellationToken.None);
        leido.EsCorrecto.ShouldBeTrue();

        InputFormatterResult resultado = await new FormateadorDeCsv().ReadAsync(Contexto(http));

        resultado.HasError.ShouldBeFalse();
        resultado.IsModelSet.ShouldBeTrue(
            "un fichero vacío es un fichero: qué le falta lo dice el lector del dialecto con su nombre, no el " +
            "enlace del marco con un «field is required»");
        resultado.Model.ShouldBeOfType<FicheroCsv>().Contenido.Length.ShouldBe(0);
    }

    private static DefaultHttpContext Peticion(Stream cuerpo, long? contentLength)
    {
        DefaultHttpContext contexto = new();
        contexto.Request.Method = HttpMethods.Post;
        contexto.Request.Path = "/api/v1/terceros/terceros/importacion";
        contexto.Request.ContentType = FormateadorDeCsv.TipoDeContenido;
        contexto.Request.Body = cuerpo;
        contexto.Request.ContentLength = contentLength;
        return contexto;
    }

    private static InputFormatterContext Contexto(HttpContext http) =>
        new(
            http,
            modelName: string.Empty,
            new ModelStateDictionary(),
            new EmptyModelMetadataProvider().GetMetadataForType(typeof(FicheroCsv)),
            (corriente, codificacion) => new StreamReader(corriente, codificacion));

    // Una corriente que cuenta cada intento de lectura y no entrega nada.
    private sealed class CorrienteQueNoSeLee : Stream
    {
        public int Lecturas { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Lecturas++;
            return 0;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Lecturas++;
            return ValueTask.FromResult(0);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
