using System.Text;
using Bastion.BuildingBlocks.Application.Importacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Application.Terceros;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.UnitTests.Identificacion;
using Shouldly;

namespace Bastion.Terceros.UnitTests.Terceros;

/// <summary>
/// Cada una de las cinco columnas de sí o no de la importación se lee en su campo, y no en el de al
/// lado ni con el valor de siempre.
/// </summary>
/// <remarks>
/// Hasta el 3.2 el recargo y la retención no los miraba ningún caso: la mutación 879, que dejaba
/// el recargo en «no», solo la vio el viaje de ida y vuelta de la exportación, contra la base. Las
/// filas tienen un solo «sí» entre las tres fiscales, y entre cliente y proveedor, uno: así
/// cualquier par de columnas cambiadas tiene una fila en la que difieren.
/// </remarks>
public sealed class CadaSiNoDeLaFilaVaASuCampoTests
{
    private static readonly string s_nif =
        IdentificadoresInventados.PersonaJuridica('B', 7654321, comoLetra: false).Valido;

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, false, false, true)]
    public void Cada_columna_llega_a_su_campo(bool cliente, bool proveedor, bool recargo, bool caja, bool irpf)
    {
        (FilaDecidida decidida, RechazosDeImportacion rechazos) = Decidir(cliente, proveedor, recargo, caja, irpf);

        rechazos.AInforme().ShouldBeEmpty();
        AltaDecidida alta = decidida.Alta.ShouldNotBeNull();
        alta.Datos.EsCliente.ShouldBe(cliente);
        alta.Datos.EsProveedor.ShouldBe(proveedor);
        alta.Regimen.RecargoDeEquivalencia.ShouldBe(recargo);
        alta.Regimen.CriterioDeCaja.ShouldBe(caja);
        alta.Regimen.SujetoARetencionIrpf.ShouldBe(irpf);
    }

    private static (FilaDecidida Decidida, RechazosDeImportacion Rechazos) Decidir(
        bool cliente, bool proveedor, bool recargo, bool caja, bool irpf)
    {
        string[] campos =
        [
            "ES", s_nif, "Foo SL", string.Empty, "Calle", string.Empty, "28013", "Pueblo", string.Empty, "ES",
            SiNo(cliente), SiNo(proveedor), string.Empty, SiNo(recargo), SiNo(caja), SiNo(irpf),
            string.Empty, string.Empty,
        ];

        campos.Length.ShouldBe(ImportacionDeTerceros.Cabecera.Count, "el arnés: una fila de la cabecera");

        byte[] fichero = Encoding.UTF8.GetBytes(
            string.Join(';', ImportacionDeTerceros.Cabecera) + "\r\n" + string.Join(';', campos) + "\r\n");

        Resultado<HojaCsv> hoja = LectorCsv.Leer(fichero, ImportacionDeTerceros.Cabecera, topeDeFilas: 10);
        hoja.EsCorrecto.ShouldBeTrue(hoja.Error?.Mensaje);

        var rechazos = new RechazosDeImportacion(ImportacionDeTerceros.Cabecera);

        return (FilasDeTerceros.Decidir(hoja.Valor.Filas.ShouldHaveSingleItem(), rechazos), rechazos);
    }

    private static string SiNo(bool valor) => valor ? "sí" : "no";
}
