using Bastion.BuildingBlocks.Application.Importacion;
using Bastion.BuildingBlocks.Contracts.Importacion;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Importacion;

public sealed class RechazosDeImportacionTests
{
    private static readonly string[] s_columnas = ["nombre", "importe", "activo"];

    [Fact]
    public void Se_agrupan_por_columna_y_motivo_en_el_orden_de_la_cabecera_con_las_lineas_ordenadas()
    {
        var rechazos = new RechazosDeImportacion(s_columnas);

        rechazos.Apuntar(9, "activo", MotivoDeRechazo.FormatoNoValido);
        rechazos.Apuntar(7, "importe", MotivoDeRechazo.FormatoNoValido);
        rechazos.Apuntar(3, "importe", MotivoDeRechazo.FormatoNoValido);
        rechazos.Apuntar(4, null, MotivoDeRechazo.NumeroDeCamposDistinto);
        rechazos.Apuntar(5, "nombre", MotivoDeRechazo.Obligatorio);
        rechazos.Apuntar(2, "importe", MotivoDeRechazo.Obligatorio);

        rechazos.AInforme().ShouldBe(
        [
            new RechazoDto(null, MotivoDeRechazo.NumeroDeCamposDistinto, [4]),
            new RechazoDto("nombre", MotivoDeRechazo.Obligatorio, [5]),
            new RechazoDto("importe", MotivoDeRechazo.Obligatorio, [2]),
            new RechazoDto("importe", MotivoDeRechazo.FormatoNoValido, [3, 7]),
            new RechazoDto("activo", MotivoDeRechazo.FormatoNoValido, [9]),
        ],
        new IgualPorContenido());
        rechazos.Rechazadas.ShouldBe(6);
    }

    [Fact]
    public void Cada_columna_de_cada_fila_se_queda_con_el_primer_motivo_y_la_fila_cuenta_una_vez()
    {
        var rechazos = new RechazosDeImportacion(s_columnas);

        rechazos.Apuntar(2, "importe", MotivoDeRechazo.Obligatorio);
        rechazos.Apuntar(2, "importe", MotivoDeRechazo.NoValido);
        rechazos.Apuntar(2, "activo", MotivoDeRechazo.FormatoNoValido);

        rechazos.AInforme().Select(rechazo => rechazo.Motivo).ShouldBe(
            [MotivoDeRechazo.Obligatorio, MotivoDeRechazo.FormatoNoValido]);
        rechazos.Rechazadas.ShouldBe(1);
        rechazos.EstaRechazada(2).ShouldBeTrue();
        rechazos.EstaRechazada(3).ShouldBeFalse();
    }

    [Fact]
    public void Una_columna_que_no_es_de_la_cabecera_no_se_apunta()
    {
        var rechazos = new RechazosDeImportacion(s_columnas);

        Should.Throw<ArgumentException>(() => rechazos.Apuntar(2, "Importe", MotivoDeRechazo.Obligatorio));
    }

    // Un registro con una lista dentro no compara la lista por contenido.
    private sealed class IgualPorContenido : IEqualityComparer<RechazoDto>
    {
        public bool Equals(RechazoDto? x, RechazoDto? y) =>
            x is not null && y is not null
            && x.Columna == y.Columna
            && x.Motivo == y.Motivo
            && x.Lineas.SequenceEqual(y.Lineas);

        public int GetHashCode(RechazoDto obj) => HashCode.Combine(obj.Columna, obj.Motivo);
    }
}
