using Bastion.Organizacion.Domain.Series;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Series;

public sealed class SerieTests
{
    private static readonly Guid s_empresa = Guid.Parse("2f6d5f4e-0000-4000-8000-000000000001");
    private static readonly Guid s_ejercicio = Guid.Parse("2f6d5f4e-0000-4000-8000-000000000002");
    private static readonly DateTimeOffset s_momento = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    private static Serie Nueva(string codigo = "FV") => Serie.Crear(
        s_empresa, s_ejercicio, TipoDeDocumento.FacturaEmitida, codigo, "{serie}/{anio}/{numero:0000}", s_momento);

    [Fact]
    public void Una_serie_nace_activa_con_el_contador_a_cero()
    {
        Serie serie = Nueva();

        serie.Estado.ShouldBe(EstadoDeSerie.Activa);
        serie.Contador.ShouldBe(0);
        serie.EmpresaId.ShouldBe(s_empresa);
        serie.EjercicioId.ShouldBe(s_ejercicio);
    }

    [Fact]
    public void El_codigo_se_normaliza_a_mayusculas_y_sin_espacios()
    {
        Nueva(" fv ").Codigo.ShouldBe("FV");
    }

    [Fact]
    public void Una_serie_sin_empresa_no_existe()
    {
        Should.Throw<ArgumentException>(() => Serie.Crear(
            Guid.Empty, s_ejercicio, TipoDeDocumento.FacturaEmitida, "FV", "{numero}", s_momento));
    }

    [Fact]
    public void Una_serie_sin_ejercicio_no_existe()
    {
        // R5 numera «por serie y ejercicio»: una serie suelta no puede garantizar correlatividad.
        Should.Throw<ArgumentException>(() => Serie.Crear(
            s_empresa, Guid.Empty, TipoDeDocumento.FacturaEmitida, "FV", "{numero}", s_momento));
    }

    [Fact]
    public void El_codigo_no_puede_pasarse_del_tope_que_deja_Verifactu()
    {
        // El `NumSerieFactura` de Veri*factu admite 60 caracteres para serie MÁS número. El tope
        // del código deja sitio al número y al separador: no es una estimación de comodidad.
        string demasiado = new('A', Serie.LongitudMaximaDeCodigo + 1);

        Should.Throw<ArgumentException>(() => Serie.Crear(
            s_empresa, s_ejercicio, TipoDeDocumento.FacturaEmitida, demasiado, "{numero}", s_momento));
    }

    [Fact]
    public void El_formato_es_obligatorio_porque_sin_el_no_se_sabe_componer_el_numero()
    {
        Should.Throw<ArgumentException>(() => Serie.Crear(
            s_empresa, s_ejercicio, TipoDeDocumento.FacturaEmitida, "FV", "   ", s_momento));
    }

    [Fact]
    public void Una_serie_recien_creada_se_puede_suprimir_porque_no_ha_numerado_nada()
    {
        Nueva().SePuedeSuprimir.ShouldBeTrue();
    }

    [Fact]
    public void Una_serie_que_ya_ha_numerado_no_se_puede_suprimir_nunca_mas()
    {
        // Borrarla dejaría documentos legales apuntando a una serie inexistente y haría
        // indemostrable la correlatividad que exige R5.
        Serie serie = Nueva();
        serie.RegistrarNumeroAsignado(1);

        serie.SePuedeSuprimir.ShouldBeFalse();
    }

    [Fact]
    public void Cerrar_una_serie_impide_que_siga_numerando_pero_conserva_el_contador()
    {
        Serie serie = Nueva();
        for (long numero = 1; numero <= 7; numero++)
        {
            serie.RegistrarNumeroAsignado(numero);
        }

        serie.Cerrar();

        serie.Estado.ShouldBe(EstadoDeSerie.Cerrada);
        serie.Contador.ShouldBe(7);
    }

    [Fact]
    public void Una_serie_cerrada_no_acepta_mas_numeros()
    {
        Serie serie = Nueva();
        serie.Cerrar();

        Should.Throw<InvalidOperationException>(() => serie.RegistrarNumeroAsignado(1));
    }

    [Fact]
    public void El_contador_solo_avanza_de_uno_en_uno_porque_R5_prohibe_los_huecos()
    {
        Serie serie = Nueva();
        serie.RegistrarNumeroAsignado(1);
        serie.RegistrarNumeroAsignado(2);

        // Saltarse el 3 sería exactamente el hueco que R5 prohíbe. El dominio lo impide aunque
        // quien llame se equivoque; es la última defensa antes de un libro registro inválido.
        Should.Throw<InvalidOperationException>(() => serie.RegistrarNumeroAsignado(4));
        serie.Contador.ShouldBe(2);
    }

    [Fact]
    public void El_contador_es_una_columna_de_la_serie_y_no_una_secuencia_de_PostgreSQL()
    {
        // Decisión de esquema, y de las que no tienen segunda oportunidad: `nextval` NO se
        // revierte al deshacer la transacción, así que una confirmación fallida dejaría un
        // hueco permanente en la numeración. R5 dice «sin huecos», y eso descarta la secuencia.
        // El número se asigna con la fila bloqueada dentro de la transacción de confirmación,
        // y eso lo hace Facturación (fase 5). Aquí solo vive el contador.
        Serie serie = Nueva();

        serie.Contador.ShouldBe(0);
        serie.RegistrarNumeroAsignado(1);
        serie.Contador.ShouldBe(1);
    }

    [Fact]
    public void Una_serie_no_lleva_estado_de_bloqueo()
    {
        // Como el ejercicio: una serie documental no contiene datos personales, así que el
        // art. 32 de la LOPDGDD no la alcanza. Lo que sí tiene es un final de vida legal
        // —dejar de numerar sin perder el histórico—, y eso es `Cerrada`.
        typeof(EstadoDeSerie).GetEnumNames()
            .ShouldBe([nameof(EstadoDeSerie.Activa), nameof(EstadoDeSerie.Cerrada)]);
    }
}
