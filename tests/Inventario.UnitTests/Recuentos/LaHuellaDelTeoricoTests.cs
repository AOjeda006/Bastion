using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bastion.Inventario.Domain.Recuentos;
using Shouldly;
using static Bastion.Inventario.UnitTests.Recuentos.ElRecuentoDeLaPrueba;

namespace Bastion.Inventario.UnitTests.Recuentos;

/// <summary>
/// La huella del teórico (ADR-0055 §2): el SHA-256 de las líneas ordenadas por su identificador,
/// cada una como <c>{id}:{teórico con seis decimales};</c>.
/// </summary>
/// <remarks>
/// <b>Es una función pura del dominio, y por eso se prueba aquí</b>: la ficha la devuelve y la
/// confirmación la compara, y si fueran dos implementaciones podrían discrepar.
/// </remarks>
public sealed class LaHuellaDelTeoricoTests
{
    private static readonly Guid s_primera = Guid.Parse("01890000-0000-7000-8000-000000000001");
    private static readonly Guid s_segunda = Guid.Parse("01890000-0000-7000-8000-000000000002");

    [Fact]
    public void Es_el_sha_256_del_texto_que_dice_el_adr()
    {
        // Escrito a mano, con el formato del ADR, para que un cambio de formato no pase por bueno
        // porque el caso y el código se calculan igual.
        const string Texto =
            "01890000000070008000000000000001:5.000000;01890000000070008000000000000002:0.125000;";

        string esperada = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Texto)));

        HuellaDelTeorico.De(new Dictionary<Guid, decimal> { [s_segunda] = 0.125m, [s_primera] = 5m })
            .ShouldBe(esperada);
    }

    [Fact]
    public void No_depende_del_orden_en_que_llegan_las_lineas()
    {
        var unas = new Dictionary<Guid, decimal> { [s_primera] = 5m, [s_segunda] = 3m };
        var otras = new Dictionary<Guid, decimal> { [s_segunda] = 3m, [s_primera] = 5m };

        HuellaDelTeorico.De(unas).ShouldBe(HuellaDelTeorico.De(otras));
    }

    [Fact]
    public void No_depende_de_la_escala_del_decimal()
    {
        // 5m y 5.000000m son el mismo número con distinta escala, y la base devuelve el segundo.
        HuellaDelTeorico.De(new Dictionary<Guid, decimal> { [s_primera] = 5m })
            .ShouldBe(HuellaDelTeorico.De(new Dictionary<Guid, decimal> { [s_primera] = 5.000000m }));
    }

    [Fact]
    public void Cambia_con_una_millonesima_del_teorico_de_una_sola_linea()
    {
        var antes = new Dictionary<Guid, decimal> { [s_primera] = 5m, [s_segunda] = 3m };
        var despues = new Dictionary<Guid, decimal> { [s_primera] = 5m, [s_segunda] = 3.000001m };

        HuellaDelTeorico.De(antes).ShouldNotBe(HuellaDelTeorico.De(despues));
    }

    [Fact]
    public void Cambia_si_el_mismo_teorico_es_de_otra_linea()
    {
        HuellaDelTeorico.De(new Dictionary<Guid, decimal> { [s_primera] = 5m, [s_segunda] = 3m })
            .ShouldNotBe(HuellaDelTeorico.De(new Dictionary<Guid, decimal> { [s_primera] = 3m, [s_segunda] = 5m }));
    }

    [Fact]
    public void Un_teorico_negativo_lleva_su_signo()
    {
        HuellaDelTeorico.De(new Dictionary<Guid, decimal> { [s_primera] = -2m })
            .ShouldNotBe(HuellaDelTeorico.De(new Dictionary<Guid, decimal> { [s_primera] = 2m }));
    }

    [Fact]
    public void No_depende_de_la_cultura_del_hilo()
    {
        // La cultura del servidor es la de producción; la del runner, en-US. La coma decimal de
        // es-ES no puede cambiar la huella (AGENTS.md, «un test fija su ambiente»).
        var teoricos = new Dictionary<Guid, decimal> { [s_primera] = 1234.5m };
        string enInvariante = EnCultura(CultureInfo.InvariantCulture, () => HuellaDelTeorico.De(teoricos));

        EnCultura(new CultureInfo("es-ES"), () => HuellaDelTeorico.De(teoricos)).ShouldBe(enInvariante);
        EnCultura(new CultureInfo("de-DE"), () => HuellaDelTeorico.De(teoricos)).ShouldBe(enInvariante);
    }

    [Fact]
    public void La_de_un_recuento_exige_el_teorico_de_todas_sus_lineas_y_de_ninguna_mas()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas));
        Dictionary<Guid, decimal> todas = TodasA(recuento, 5m);

        recuento.HuellaDelTeorico(todas).ShouldBe(HuellaDelTeorico.De(todas));

        var deUna = new Dictionary<Guid, decimal> { [recuento.Lineas[0].Id] = 5m };
        Should.Throw<ArgumentException>(() => recuento.HuellaDelTeorico(deUna));

        var conOtra = new Dictionary<Guid, decimal>(todas) { [Guid.CreateVersion7()] = 1m };
        Should.Throw<ArgumentException>(() => recuento.HuellaDelTeorico(conOtra));
    }

    [Fact]
    public void Las_lineas_con_el_teorico_cambiado_son_las_contadas_cuyo_teorico_ya_no_es_el_de_entonces()
    {
        Recuento recuento = Abrir(
            Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas), Precargada(Estanteria, Taladros));

        recuento.Contar(LaDe(recuento, Tornillos).Id, 4m, 5m);
        recuento.Contar(LaDe(recuento, Tuercas).Id, 4m, 5m);

        Dictionary<Guid, decimal> ahora = TodasA(recuento, 5m);
        ahora[LaDe(recuento, Tuercas).Id] = 3m;
        ahora[LaDe(recuento, Taladros).Id] = 9m;

        recuento.LineasConElTeoricoCambiado(ahora).ShouldBe(
            [LaDe(recuento, Tuercas)],
            "la de taladros no está contada, así que no tiene un teórico de cuando se contó con el que comparar");
    }

    [Fact]
    public void Las_lineas_que_suben_son_las_contadas_por_encima_del_teorico()
    {
        Recuento recuento = Abrir(
            Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas), Precargada(Estanteria, Taladros));

        recuento.Contar(LaDe(recuento, Tornillos).Id, 6m, 5m);
        recuento.Contar(LaDe(recuento, Tuercas).Id, 4m, 5m);
        recuento.Contar(LaDe(recuento, Taladros).Id, 5m, 5m);

        recuento.LineasQueSuben(TodasA(recuento, 5m)).ShouldBe([LaDe(recuento, Tornillos)]);
    }

    private static T EnCultura<T>(CultureInfo cultura, Func<T> accion)
    {
        CultureInfo antes = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = cultura;
            return accion();
        }
        finally
        {
            CultureInfo.CurrentCulture = antes;
        }
    }
}
