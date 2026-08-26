using System.Buffers.Binary;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.Identidad.Application.Sesiones;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Acceso;

/// <summary>
/// Con qué se resumen las contraseñas, comprobado sobre <b>la implementación que el host
/// registra</b> y desmontando el resumen que produce.
/// </summary>
/// <remarks>
/// <para>
/// El ADR-0008 dice PBKDF2-HMAC-SHA512, sal de 128 bits, clave de 256 y 100 000 iteraciones. Un
/// ADR es un documento: si el paquete cambia sus valores por omisión, el documento sigue diciendo
/// lo mismo y deja de ser verdad, sin que nada avise. <b>Los parámetros están aquí, en el test</b>,
/// para que ese cambio salga en rojo y el ADR se corrija con él.
/// </para>
/// <para>
/// El formato de la versión 3 de ese hasher es público y estable: un byte de marca, y tres enteros
/// de 32 bits en orden de red con la función pseudoaleatoria, las iteraciones y el tamaño de la
/// sal. Después van la sal y la clave derivada. Es lo que se desmonta abajo.
/// </para>
/// </remarks>
public sealed class ElResumenDeContrasenasTests : IDisposable
{
    private const byte MarcaDeVersion3 = 0x01;
    private const int HmacSha512 = 2;
    private const int Iteraciones = 100_000;
    private const int BytesDeSal = 16;
    private const int BytesDeClave = 32;

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void El_resumen_lleva_dentro_los_parametros_que_declara_el_ADR_0008()
    {
        (byte marca, int prf, int iteraciones, int sal, int clave) = Desmontar(
            Hasher().Hashear("una contraseña cualquiera"));

        marca.ShouldBe(MarcaDeVersion3);
        prf.ShouldBe(HmacSha512, "el ADR-0008 dice HMAC-SHA512");
        iteraciones.ShouldBe(Iteraciones, "el ADR-0008 dice 100 000 iteraciones");
        sal.ShouldBe(BytesDeSal, "el ADR-0008 dice 128 bits de sal");
        clave.ShouldBe(BytesDeClave, "el ADR-0008 dice 256 bits de clave derivada");
    }

    [Fact]
    public void Dos_resumenes_de_la_MISMA_contrasena_son_distintos()
    {
        IHasherDeContrasenas hasher = Hasher();

        string primero = hasher.Hashear("la misma contraseña");
        string segundo = hasher.Hashear("la misma contraseña");

        // La sal es por contraseña, no por instalación. Sin ella, dos personas con la misma
        // contraseña tienen la misma fila, y una tabla precalculada las abre las dos de una vez.
        primero.ShouldNotBe(segundo);
    }

    [Fact]
    public void El_resumen_comprueba_la_contrasena_buena_y_rechaza_cualquier_otra()
    {
        IHasherDeContrasenas hasher = Hasher();
        string hash = hasher.Hashear("Una contraseña larga y con espacios");

        hasher.Comprobar(hash, "Una contraseña larga y con espacios")
            .ShouldBe(ResultadoDeComprobacion.Correcta);

        hasher.Comprobar(hash, "Una contraseña larga y con espacios ")
            .ShouldBe(ResultadoDeComprobacion.Incorrecta);

        hasher.Comprobar(hash, "una contraseña larga y con espacios")
            .ShouldBe(ResultadoDeComprobacion.Incorrecta);
    }

    [Fact]
    public void El_resumen_de_relleno_cuesta_lo_mismo_que_uno_de_verdad_y_no_lo_abre_nadie()
    {
        IHasherDeContrasenas hasher = Hasher();

        // Mismos parámetros que uno de verdad; si no, tardaría distinto y el cronómetro volvería a
        // distinguir la cuenta que existe de la que no (ADR-0008, punto 4).
        Desmontar(hasher.HashDeRelleno).ShouldBe(Desmontar(hasher.Hashear("da igual cuál")));

        // Y contra él no entra nadie: la contraseña de la que salió no la conoce ni este proceso.
        hasher.Comprobar(hasher.HashDeRelleno, string.Empty)
            .ShouldBe(ResultadoDeComprobacion.Incorrecta);
        hasher.Comprobar(hasher.HashDeRelleno, "relleno")
            .ShouldBe(ResultadoDeComprobacion.Incorrecta);
    }

    [Fact]
    public void El_hasher_es_uno_solo_para_todo_el_proceso()
    {
        using IServiceScope primero = _api.Services.CreateScope();
        using IServiceScope segundo = _api.Services.CreateScope();

        // Singleton, y no por ahorrar: el resumen de relleno se calcula en el constructor y cuesta
        // lo que cuesta comprobar una contraseña. Con un servicio por petición, cada intento de
        // acceso pagaría ese cálculo además del suyo.
        primero.ServiceProvider.GetRequiredService<IHasherDeContrasenas>()
            .ShouldBeSameAs(segundo.ServiceProvider.GetRequiredService<IHasherDeContrasenas>());
    }

    private static (byte Marca, int Prf, int Iteraciones, int Sal, int Clave) Desmontar(string hash)
    {
        byte[] bytes = Convert.FromBase64String(hash);

        int sal = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(9, 4));

        return (
            bytes[0],
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(1, 4)),
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(5, 4)),
            sal,
            bytes.Length - 13 - sal);
    }

    private IHasherDeContrasenas Hasher() =>
        _api.Services.GetRequiredService<IHasherDeContrasenas>();
}
