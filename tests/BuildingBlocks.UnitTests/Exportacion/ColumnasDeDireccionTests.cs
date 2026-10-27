using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Contracts.Direcciones;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Exportacion;

/// <summary>
/// Las seis columnas de una dirección: los nombres de la importación de terceros con el prefijo de
/// quien la lleva, y seis campos vacíos cuando no hay dirección.
/// </summary>
public sealed class ColumnasDeDireccionTests
{
    private static readonly IReadOnlyList<ColumnaCsv<Sitio>> s_columnas =
        ColumnasDeDireccion.De<Sitio>("domicilio", sitio => sitio.Direccion);

    [Fact]
    public void Las_columnas_son_las_seis_de_la_importacion_con_el_prefijo()
    {
        s_columnas.Select(columna => columna.Nombre).ShouldBe(
        [
            "domicilio_calle",
            "domicilio_numero",
            "domicilio_codigo_postal",
            "domicilio_poblacion",
            "domicilio_subdivision",
            "domicilio_pais",
        ]);
    }

    [Fact]
    public void Cada_columna_lleva_su_campo_como_texto()
    {
        Sitio sitio = new(new DireccionDto
        {
            Calle = "=Calle",
            Numero = "3 B",
            CodigoPostal = "28013",
            Poblacion = "Madrid",
            Subdivision = "M",
            Pais = "ES",
        });

        s_columnas.Select(columna => EscritorCsv.Escribir(columna.Campo(sitio))).ShouldBe(
            ["'=Calle", "3 B", "28013", "Madrid", "M", "ES"],
            "la calle la escribe una persona, y lleva la regla del apóstrofo");
    }

    [Fact]
    public void Sin_direccion_salen_seis_campos_vacios()
    {
        s_columnas.Select(columna => EscritorCsv.Escribir(columna.Campo(new Sitio(null))))
            .ShouldBe(Enumerable.Repeat(string.Empty, 6));
    }

    private sealed record Sitio(DireccionDto? Direccion);
}
