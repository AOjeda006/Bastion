using Bastion.Inventario.Domain.Ajustes;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Ajustes;

/// <summary>Lo que un ajuste exige para abrirse: empresa, almacén y un motivo escrito.</summary>
/// <remarks>
/// <para>
/// <b>El motivo es la única de las tres que no puede comprobar nadie más.</b> La empresa y el
/// almacén los vuelven a mirar los puertos del 2.2 contra sus tablas, y la columna los sostiene
/// con un <c>NOT NULL</c>; el motivo, en cambio, es texto libre que solo esta fábrica juzga, y es
/// lo único que queda para entender un descuadre dentro de dos años. Un ajuste con el motivo en
/// blanco es un descuadre sin explicación, y la base de datos lo aceptaría encantada.
/// </para>
/// <para>
/// <b>Las fronteras del largo se miran en el límite y no cerca</b>: el que cabe justo y el
/// primero que no. Un caso con 10 y otro con 5.000 saldría verde con cualquier tope entre medias,
/// incluido uno que no coincidiera con el de la columna — y entonces el rechazo llegaría desde
/// PostgreSQL, como un 500 en vez de como una validación.
/// </para>
/// </remarks>
public sealed class ElMotivoDeUnAjusteEsObligatorioTests
{
    private static readonly DateTimeOffset s_momento =
        new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Un ajuste sin empresa o sin almacén no existe.</summary>
    /// <remarks>
    /// Los dos en el mismo caso y comprobando el <c>ParamName</c>, que es lo que los distingue:
    /// una guarda que mirase dos veces la empresa dejaría el almacén vacío pasar, y el mensaje
    /// diría «empresa» sobre un ajuste que sí la tenía.
    /// </remarks>
    [Fact]
    public void Sin_empresa_o_sin_almacen_no_hay_ajuste()
    {
        Should.Throw<ArgumentException>(() => Abrir(empresaId: Guid.Empty))
            .ParamName.ShouldBe("empresaId");

        Should.Throw<ArgumentException>(() => Abrir(almacenId: Guid.Empty))
            .ParamName.ShouldBe("almacenId");
    }

    /// <summary>Un motivo en blanco no es un motivo, ni aunque lleve espacios dentro.</summary>
    /// <remarks>
    /// <b>La cadena de espacios es la que importa.</b> La vacía la pararía un
    /// <c>string.IsNullOrEmpty</c>, un <c>Length == 0</c> o cualquier cosa parecida; el texto de
    /// tres espacios solo lo para mirar el contenido, y es exactamente lo que llega desde un
    /// formulario en el que alguien pulsó la barra espaciadora para saltarse el campo.
    /// </remarks>
    [Fact]
    public void Un_motivo_en_blanco_no_es_un_motivo()
    {
        Should.Throw<ArgumentException>(() => Abrir(motivo: string.Empty))
            .ParamName.ShouldBe("motivo");

        Should.Throw<ArgumentException>(() => Abrir(motivo: "   "))
            .ParamName.ShouldBe("motivo");

        Should.Throw<ArgumentException>(() => Abrir(motivo: null!))
            .ParamName.ShouldBe("motivo");
    }

    /// <summary>El motivo se guarda sin los espacios de los bordes.</summary>
    /// <remarks>
    /// No es cosmética: sin recortar, «  Recuento  » y «Recuento» son dos motivos distintos para
    /// cualquier búsqueda o agrupación posterior, y el que los tecleó cree haber escrito el mismo.
    /// </remarks>
    [Fact]
    public void El_motivo_se_guarda_recortado() =>
        Abrir(motivo: "  Recuento de marzo  ").Motivo.ShouldBe("Recuento de marzo");

    /// <summary>El largo del motivo se corta exactamente donde lo dice la constante.</summary>
    /// <remarks>
    /// <b>El recorte va antes del largo, y esta es la única comprobación que lo ve.</b> El caso de
    /// los espacios de sobra tiene un motivo que cabría de todas formas; aquí el texto cabe justo
    /// <i>después</i> de recortar y no antes, así que medir la cadena original lo rechazaría. Es
    /// el mismo formulario del caso anterior con un campo lleno hasta el tope.
    /// </remarks>
    [Fact]
    public void El_motivo_cabe_justo_en_el_limite_y_no_uno_mas()
    {
        string justo = new('x', Ajuste.LargoDelMotivo);

        Abrir(motivo: justo).Motivo.Length.ShouldBe(Ajuste.LargoDelMotivo);

        Abrir(motivo: $"  {justo}  ").Motivo.Length.ShouldBe(
            Ajuste.LargoDelMotivo,
            "el recorte va ANTES de medir: si se midiera la cadena de entrada, este motivo —que " +
            "cabe justo— se rechazaría por cuatro espacios");

        Should.Throw<ArgumentException>(() => Abrir(motivo: new string('x', Ajuste.LargoDelMotivo + 1)))
            .ParamName.ShouldBe("motivo");
    }

    private static Ajuste Abrir(
        Guid? empresaId = null,
        Guid? almacenId = null,
        string motivo = "Recuento de marzo") => Ajuste.Abrir(
            empresaId ?? Guid.CreateVersion7(),
            almacenId ?? Guid.CreateVersion7(),
            new DateOnly(2026, 3, 14),
            motivo,
            s_momento);
}
