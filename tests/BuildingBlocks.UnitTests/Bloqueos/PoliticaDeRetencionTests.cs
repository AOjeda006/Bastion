using System.Globalization;
using Bastion.BuildingBlocks.Domain.Bloqueos;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Bloqueos;

/// <summary>
/// Cuándo vence un bloqueo del artículo 32, y qué pasa con un plazo mal configurado.
/// </summary>
/// <remarks>
/// <b>El caso que de verdad importa aquí es el motivo nuevo.</b> Los dos motivos de hoy tienen su
/// respuesta escrita; lo que hay que asegurar es que un tercero no herede ninguna de las dos en
/// silencio, porque heredar «no vence» deja la conservación indefinida —que es la infracción por el
/// otro lado— y heredar «vence» pondría a destruir un dato mercantil que hay que guardar.
/// </remarks>
public sealed class PoliticaDeRetencionTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    private static Bloqueo Bloqueado(MotivoDeBloqueo motivo) =>
        Bloqueo.Ninguno().Bloquear(motivo, s_momento);

    [Fact]
    public void Sin_variable_el_plazo_es_el_del_articulo_30_del_Codigo_de_Comercio()
    {
        PoliticaDeRetencion.PorOmision().AniosDeSupresion.ShouldBe(6);
        PoliticaDeRetencion.De(null).AniosDeSupresion.ShouldBe(6);
        PoliticaDeRetencion.De("   ").AniosDeSupresion.ShouldBe(6);
    }

    [Fact]
    public void Una_supresion_vence_al_cumplirse_el_plazo_desde_la_fecha_del_bloqueo()
    {
        PoliticaDeRetencion politica = PoliticaDeRetencion.De("4");

        politica.VenceEn(Bloqueado(MotivoDeBloqueo.SupresionSolicitada))
            .ShouldBe(s_momento.AddYears(4));
    }

    [Fact]
    public void Un_cese_de_uso_no_vence_nunca_y_eso_no_es_una_infraccion()
    {
        // El nulo dice «este bloqueo no caduca», que es cierto y distinto de «no se sabe cuándo».
        // Un almacén retirado se conserva por razón contable: el histórico de valoración apunta a
        // él para siempre, y sus datos no son de nadie.
        PoliticaDeRetencion.PorOmision()
            .VenceEn(Bloqueado(MotivoDeBloqueo.CeseDeUso))
            .ShouldBeNull();
    }

    [Fact]
    public void Lo_que_no_esta_bloqueado_no_vence()
    {
        PoliticaDeRetencion.PorOmision().VenceEn(Bloqueo.Ninguno()).ShouldBeNull();
    }

    [Fact]
    public void Un_motivo_nuevo_no_hereda_respuesta_TIENE_que_decidirla()
    {
        // El caso vale porque el enumerado se puede ampliar sin tocar esta clase, y ese es el
        // descuido que hay que parar: un valor que no existe hoy imita al que existirá mañana.
        Bloqueo inventado = Bloqueo.Ninguno().Bloquear((MotivoDeBloqueo)99, s_momento);

        Should.Throw<InvalidOperationException>(
            () => PoliticaDeRetencion.PorOmision().VenceEn(inventado),
            "un motivo de bloqueo sin respuesta escrita ha heredado una en silencio, y las dos " +
            "que puede heredar están mal: «no vence» deja la conservación indefinida y «vence» " +
            "pone a destruir lo que hay que guardar");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("31")]
    [InlineData("-1")]
    [InlineData("seis")]
    [InlineData("6,5")]
    [InlineData("6.5")]
    [InlineData(" 6 ")]
    public void Un_plazo_PUESTO_y_mal_para_el_arranque(string plazo)
    {
        // Ausente vale y significa «el de omisión»; puesto y mal, no. Es alguien intentando
        // configurar algo y consiguiendo otra cosa, y el sitio donde eso se nota es el arranque,
        // no el primer listado de bloqueados de dentro de seis meses.
        InvalidOperationException fallo = Should.Throw<InvalidOperationException>(
            () => PoliticaDeRetencion.De(plazo));

        fallo.Message.ShouldContain(PoliticaDeRetencion.VariableDelPlazo);
    }

    [Fact]
    public void El_plazo_se_lee_igual_en_cualquier_cultura()
    {
        // La misma imagen de contenedor arranca en un servidor en castellano y en uno en inglés, y
        // el plazo que lee tiene que ser el mismo. Sin cultura invariante, este test sale verde en
        // la máquina que lo escribió y rojo en la que no.
        CultureInfo anterior = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            PoliticaDeRetencion.De("6").AniosDeSupresion.ShouldBe(6);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public void Los_topes_declarados_son_los_que_se_aplican()
    {
        PoliticaDeRetencion.De(PoliticaDeRetencion.AniosMinimos.ToString(CultureInfo.InvariantCulture))
            .AniosDeSupresion.ShouldBe(PoliticaDeRetencion.AniosMinimos);

        PoliticaDeRetencion.De(PoliticaDeRetencion.AniosMaximos.ToString(CultureInfo.InvariantCulture))
            .AniosDeSupresion.ShouldBe(PoliticaDeRetencion.AniosMaximos);
    }
}
