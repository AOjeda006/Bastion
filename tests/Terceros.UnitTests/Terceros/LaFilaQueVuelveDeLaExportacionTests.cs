using System.Text;
using Bastion.BuildingBlocks.Application.Importacion;
using Bastion.BuildingBlocks.Contracts.Importacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Application.Terceros;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.UnitTests.Identificacion;
using Shouldly;

namespace Bastion.Terceros.UnitTests.Terceros;

/// <summary>
/// Una fila con el apóstrofo que pone la exportación entra con sus textos como eran, y el apóstrofo
/// solo se quita de un texto, y solo el que se puso (ADR-0075).
/// </summary>
/// <remarks>
/// Se mira la decisión de la fila, que es la que lee los campos, y no el alta, que va contra la base:
/// lo que sale de aquí es lo que se guarda.
/// </remarks>
public sealed class LaFilaQueVuelveDeLaExportacionTests
{
    private static readonly string s_nif =
        IdentificadoresInventados.PersonaJuridica('B', 1234567, comoLetra: false).Valido;

    [Fact]
    public void Los_textos_vuelven_sin_el_apostrofo_que_les_puso_la_exportacion()
    {
        (FilaDecidida decidida, RechazosDeImportacion rechazos) = Decidir(
            razonSocial: "'=Foo SL", nombreComercial: "''=Comercial", calle: "'+Calle", poblacion: "'-Pueblo");

        rechazos.AInforme().ShouldBeEmpty();
        CrearTerceroDto datos = decidida.Alta.ShouldNotBeNull().Datos;
        datos.RazonSocial.ShouldBe("=Foo SL");
        datos.NombreComercial.ShouldBe("'=Comercial", "el apóstrofo de un texto que ya lo traía, también vuelve");
        datos.DomicilioFiscal.Calle.ShouldBe("+Calle");
        datos.DomicilioFiscal.Poblacion.ShouldBe("-Pueblo");
    }

    /// <summary>La pareja: un apóstrofo que no lo sigue uno de los siete lo escribió alguien, y se queda.</summary>
    [Fact]
    public void Un_apostrofo_que_no_puso_la_exportacion_se_queda()
    {
        (FilaDecidida decidida, RechazosDeImportacion rechazos) = Decidir(
            razonSocial: "'Hola SL", nombreComercial: "Comercial", calle: "Calle", poblacion: "Pueblo");

        rechazos.AInforme().ShouldBeEmpty();
        decidida.Alta.ShouldNotBeNull().Datos.RazonSocial.ShouldBe("'Hola SL");
    }

    /// <summary>
    /// Un importe no lo escribe una persona: la exportación no le pone apóstrofo, y uno que lo lleva no
    /// es del dialecto. Si se le quitara, <c>'-5</c> se leería como un número.
    /// </summary>
    [Fact]
    public void Un_importe_con_apostrofo_no_se_lee()
    {
        (_, RechazosDeImportacion rechazos) = Decidir(
            razonSocial: "Foo SL", nombreComercial: "Comercial", calle: "Calle", poblacion: "Pueblo", limite: "'-5");

        RechazoDto rechazo = rechazos.AInforme().ShouldHaveSingleItem();
        rechazo.Columna.ShouldBe(ImportacionDeTerceros.LimiteCredito);
        rechazo.Motivo.ShouldBe(MotivoDeRechazo.FormatoNoValido);
        rechazo.Lineas.ShouldBe([2]);
    }

    private static (FilaDecidida Decidida, RechazosDeImportacion Rechazos) Decidir(
        string razonSocial, string nombreComercial, string calle, string poblacion, string limite = "")
    {
        string[] campos =
        [
            "ES", s_nif, razonSocial, nombreComercial, calle, string.Empty, "28013", poblacion, string.Empty, "ES",
            "sí", "no", string.Empty, "no", "no", "no", limite, limite.Length == 0 ? string.Empty : "EUR",
        ];

        campos.Length.ShouldBe(ImportacionDeTerceros.Cabecera.Count, "el arnés: una fila de la cabecera");

        byte[] fichero = Encoding.UTF8.GetBytes(
            string.Join(';', ImportacionDeTerceros.Cabecera) + "\r\n" + string.Join(';', campos) + "\r\n");

        Resultado<HojaCsv> hoja = LectorCsv.Leer(fichero, ImportacionDeTerceros.Cabecera, topeDeFilas: 10);
        hoja.EsCorrecto.ShouldBeTrue(hoja.Error?.Mensaje);

        var rechazos = new RechazosDeImportacion(ImportacionDeTerceros.Cabecera);

        return (FilasDeTerceros.Decidir(hoja.Valor.Filas.ShouldHaveSingleItem(), rechazos), rechazos);
    }
}
