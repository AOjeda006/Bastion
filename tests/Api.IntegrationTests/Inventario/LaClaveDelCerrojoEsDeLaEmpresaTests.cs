using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Las dos claves del cerrojo de las instantáneas: el espacio de Inventario y la empresa plegada a
/// 32 bits (ADR-0063 §5, desde el ítem 2.14).
/// </summary>
/// <remarks>
/// <para>
/// <b>El plegado se fija con valores escritos a mano</b>, y no comparándolo con otra forma de
/// calcularlo. Dos instancias desplegadas con dos plegados distintos tomarían dos cerrojos
/// distintos para la misma empresa, y el recálculo de una no esperaría a las confirmaciones de la
/// otra: nada lo diría. Cambiar el plegado tiene que poner esto rojo, y obligar a decidirlo.
/// </para>
/// <para>
/// <b>En el carril rápido</b>, en este ensamblado porque es el que ve lo interno de la
/// infraestructura de Inventario. Que las sentencias usen estas claves lo prueban los casos del
/// cerrojo, contra PostgreSQL, en <c>LasExistenciasSonLaSumaDelLibroTests</c>.
/// </para>
/// </remarks>
public sealed class LaClaveDelCerrojoEsDeLaEmpresaTests
{
    /// <summary>La empresa, y su segunda clave escrita a mano en hexadecimal.</summary>
    public static TheoryData<string, uint> Plegados { get; } = new()
    {
        // Las cuatro palabras, en little-endian: 0x01234567, 0xcdef89ab, 0x67452301 y 0xefcdab89.
        { "01234567-89ab-cdef-0123-456789abcdef", 0x44444444u },
        // Una sola palabra llena, la primera: sale negativa, y es la que fija el signo.
        { "ffffffff-0000-0000-0000-000000000000", 0xffffffffu },
        { "00000000-0000-0000-0000-000000000000", 0x00000000u },
    };

    [Theory]
    [MemberData(nameof(Plegados))]
    public void La_empresa_se_pliega_con_el_xor_de_sus_cuatro_palabras(string empresa, uint esperado)
    {
        ClaveDelCerrojoDeLasInstantaneas.DeLaEmpresa(Guid.Parse(empresa, CultureInfo.InvariantCulture))
            .ShouldBe(unchecked((int)esperado), $"el plegado de {empresa}");
    }

    [Fact]
    public void El_espacio_es_Inv1_en_ascii()
    {
        BinaryPrimitives.ReadInt32BigEndian(Encoding.ASCII.GetBytes("Inv1"))
            .ShouldBe(ClaveDelCerrojoDeLasInstantaneas.Espacio);
    }

    [Fact]
    public void Dos_empresas_con_las_mismas_palabras_en_otro_orden_comparten_cerrojo()
    {
        var una = Guid.Parse("0a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9", CultureInfo.InvariantCulture);

        byte[] bytes = una.ToByteArray();

        // La primera palabra y la última, cambiadas de sitio.
        byte[] permutados = [.. bytes[12..], .. bytes[4..12], .. bytes[..4]];
        var otra = new Guid(permutados);

        otra.ShouldNotBe(una, "son dos empresas distintas");

        // Y se esperan entre sí: es lo que cuesta un plegado, paralelismo y no corrección.
        ClaveDelCerrojoDeLasInstantaneas.DeLaEmpresa(otra)
            .ShouldBe(ClaveDelCerrojoDeLasInstantaneas.DeLaEmpresa(una));
    }
}
