using Bastion.BuildingBlocks.Domain.Resultados;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Resultados;

/// <summary>
/// <c>api-rest.md</c> pide que un conflicto devuelva el estado actual del recurso. El ADR-0055 §11
/// lo pone en el error, y el borde lo publica como la extensión <c>actual</c>.
/// </summary>
public sealed class ElEstadoActualTests
{
    [Fact]
    public void Un_error_no_lleva_estado_actual_si_nadie_se_lo_pone()
    {
        var conflicto = ErrorDeOperacion.Conflicto("recuento-teorico-cambiado", "Vuelva a mirarlo.");

        conflicto.Actual.ShouldBeNull();
    }

    [Fact]
    public void El_conflicto_y_la_regla_de_negocio_lo_llevan_y_lo_demas_del_error_no_cambia()
    {
        object estado = new { Huella = "abc" };

        ErrorDeOperacion conflicto = ErrorDeOperacion
            .Conflicto("recuento-teorico-cambiado", "Vuelva a mirarlo.")
            .ConElEstadoActual(estado);

        ErrorDeOperacion regla = ErrorDeOperacion
            .ReglaDeNegocio("recuento-con-lineas-sin-contar", "Cuente las que faltan.")
            .ConElEstadoActual(estado);

        conflicto.Actual.ShouldBeSameAs(estado);
        conflicto.Codigo.ShouldBe("recuento-teorico-cambiado");
        conflicto.Mensaje.ShouldBe("Vuelva a mirarlo.");
        conflicto.Tipo.ShouldBe(TipoDeError.Conflicto);

        regla.Actual.ShouldBeSameAs(estado);
        regla.Tipo.ShouldBe(TipoDeError.ReglaDeNegocio);
    }

    [Fact]
    public void Poner_el_estado_no_toca_el_error_de_partida()
    {
        // El error de partida puede ser un valor compartido, como los de una clase de errores. Si
        // la copia lo tocara, el siguiente que lo usara saldría con el estado de otro.
        var original = ErrorDeOperacion.Conflicto("recuento-teorico-cambiado", "Vuelva a mirarlo.");

        _ = original.ConElEstadoActual(new { Huella = "abc" });

        original.Actual.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(LasClasesQueNoCuentanEstado))]
    public void Los_demas_errores_no_cuentan_estado_y_quien_lo_intenta_esta_mal_escrito(
        TipoDeError tipo)
    {
        ErrorDeOperacion error = UnErrorDe(tipo);

        Should.Throw<InvalidOperationException>(() => error.ConElEstadoActual(new { Huella = "abc" }));
    }

    [Fact]
    public void Un_estado_nulo_no_es_un_estado()
    {
        var conflicto = ErrorDeOperacion.Conflicto("recuento-teorico-cambiado", "Vuelva a mirarlo.");

        Should.Throw<ArgumentNullException>(() => conflicto.ConElEstadoActual(null!));
    }

    /// <summary>Todas las clases de error menos las dos que cuentan estado.</summary>
    /// <remarks>
    /// Sale del enumerado, así que una clase nueva entra sola en la teoría, y
    /// <see cref="UnErrorDe"/> la pone roja hasta que alguien diga cómo se construye.
    /// </remarks>
    public static TheoryData<TipoDeError> LasClasesQueNoCuentanEstado()
    {
        TheoryData<TipoDeError> clases = [];

        foreach (TipoDeError tipo in Enum.GetValues<TipoDeError>())
        {
            if (tipo is not (TipoDeError.Conflicto or TipoDeError.ReglaDeNegocio))
            {
                clases.Add(tipo);
            }
        }

        return clases;
    }

    private static ErrorDeOperacion UnErrorDe(TipoDeError tipo) => tipo switch
    {
        TipoDeError.Validacion => ErrorDeOperacion.Validacion("datos-no-validos", "Revise."),
        TipoDeError.NoAutenticado => ErrorDeOperacion.NoAutenticado("sesion-caducada", "Vuelva a entrar."),
        TipoDeError.PermisoDenegado => ErrorDeOperacion.PermisoDenegado("sin-permiso", "Pida permiso."),
        TipoDeError.NoEncontrado => ErrorDeOperacion.NoEncontrado("recuento-no-encontrado", "No existe."),
        TipoDeError.VersionObsoleta => ErrorDeOperacion.VersionObsoleta("version-obsoleta", "Recargue."),
        TipoDeError.FaltaLaPrecondicion =>
            ErrorDeOperacion.FaltaLaPrecondicion("falta-if-match", "Mande la versión."),
        TipoDeError.DemasiadoGrande =>
            ErrorDeOperacion.DemasiadoGrande("cuerpo-demasiado-grande", "Mande menos."),
        _ => throw new ArgumentOutOfRangeException(
            nameof(tipo), tipo, "Una clase de error nueva: decida si cuenta el estado actual."),
    };
}
