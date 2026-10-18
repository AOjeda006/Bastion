using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Domain.Unidades;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// El puerto de las conversiones del 2.17 contra PostgreSQL de verdad (ADR-0065 §1): el par exacto,
/// con su factor; ni su inverso ni lo que no está; y la retirada, con su marca.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué contra la base y no con un doble</b>, como sus hermanos de
/// <see cref="LosPuertosDeLecturaTests"/>: el alta del ajuste no tiene otra forma de saber el factor,
/// y un doble contestaría lo que se le diga. Lo que hay que ver es que la consulta trae las filas
/// pedidas y se queda solo con ellas.
/// </para>
/// <para>
/// <b>Cada caso da de alta sus propias unidades</b>, con códigos que no usa ningún otro, y pregunta
/// por los identificadores que acaba de crear. Y el contexto de <see cref="PostgresDeVerdad"/> lanza
/// si alguien le pregunta la empresa: que el puerto conteste es la prueba de que la conversión es un
/// maestro de la instalación.
/// </para>
/// </remarks>
[Trait("Category", "Integracion")]
[Collection(ColeccionDePostgres.Nombre)]
public sealed class LaConsultaDeConversionesTests(PostgresDeVerdad postgres)
{
    private static readonly DateTimeOffset s_momento = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Contesta_el_par_exacto_con_su_factor_y_ni_su_inverso_ni_lo_que_no_esta()
    {
        var caja = UnidadMedida.Crear("PCC", "Caja del puerto de conversiones", 0, s_momento);
        var unidad = UnidadMedida.Crear("PCU", "Unidad del puerto de conversiones", 0, s_momento);
        var palet = UnidadMedida.Crear("PCP", "Palé del puerto de conversiones", 0, s_momento);
        var docena = ConversionUM.Crear(caja.Id, unidad.Id, 12m, s_momento);
        var cajasPorPalet = ConversionUM.Crear(palet.Id, caja.Id, 40m, s_momento);

        await GuardarAsync(contexto =>
        {
            contexto.UnidadesDeMedida.AddRange(caja, unidad, palet);
            contexto.ConversionesDeUnidades.AddRange(docena, cajasPorPalet);
        });

        ParDeUnidades pedido = new(caja.Id, unidad.Id);
        ParDeUnidades inverso = new(unidad.Id, caja.Id);
        ParDeUnidades encadenado = new(palet.Id, unidad.Id);

        IReadOnlyDictionary<ParDeUnidades, ConversionResuelta> contestadas =
            await ResolverAsync(pedido, inverso, encadenado, pedido);

        // La pareja de la ausencia: un puerto que no contestara nada pasaría las dos de abajo.
        contestadas.Keys.ShouldBe([pedido], "solo se contesta el par que existe, y una vez");
        contestadas[pedido].ShouldBe(new ConversionResuelta(12m, EstaRetirada: false));

        contestadas.ShouldNotContainKey(
            inverso, "la inversa redondeada sería una conversión que nadie ha escrito (ADR-0023)");
        contestadas.ShouldNotContainKey(
            encadenado, "del palé a la unidad hay dos conversiones, y no se encadenan (ADR-0023)");
    }

    [Fact]
    public async Task La_conversion_retirada_se_contesta_con_su_marca_y_su_factor()
    {
        var saco = UnidadMedida.Crear("PCS", "Saco del puerto de conversiones", 0, s_momento);
        var kilo = UnidadMedida.Crear("PCK", "Kilo del puerto de conversiones", 3, s_momento);
        var veinticinco = ConversionUM.Crear(saco.Id, kilo.Id, 25m, s_momento);

        await GuardarAsync(contexto =>
        {
            contexto.UnidadesDeMedida.AddRange(saco, kilo);
            contexto.ConversionesDeUnidades.Add(veinticinco);
        });

        ParDeUnidades par = new(saco.Id, kilo.Id);

        (await ResolverAsync(par))[par].ShouldBe(
            new ConversionResuelta(25m, EstaRetirada: false),
            "recién dada de alta no está retirada: sin esta mitad, un puerto que contestara " +
            "siempre la marca pasaría la de abajo");

        await GuardarAsync(contexto =>
            contexto.ConversionesDeUnidades.Single(fila => fila.Id == veinticinco.Id).Retirar());

        (await ResolverAsync(par))[par].ShouldBe(
            new ConversionResuelta(25m, EstaRetirada: true),
            "retirada, sigue en el diccionario con su factor: quien pregunta decide qué hace con " +
            "ella, y el alta del ajuste la rechaza con su propio código (ADR-0065 §2)");
    }

    [Fact]
    public async Task Sin_pares_no_contesta_nada()
    {
        await using OrganizacionDbContext contexto = postgres.AbrirContexto();

        (await new ConsultaDeConversiones(contexto).DeLosParesAsync([], CancellationToken.None))
            .ShouldBeEmpty();
    }

    private async Task<IReadOnlyDictionary<ParDeUnidades, ConversionResuelta>> ResolverAsync(
        params ParDeUnidades[] pares)
    {
        await using OrganizacionDbContext contexto = postgres.AbrirContexto();

        return await new ConsultaDeConversiones(contexto).DeLosParesAsync(pares, CancellationToken.None);
    }

    /// <summary>Da de alta por el mismo camino que la aplicación: el agregado y su contexto.</summary>
    private async Task GuardarAsync(Action<OrganizacionDbContext> alta)
    {
        await using OrganizacionDbContext contexto = postgres.AbrirContexto();

        alta(contexto);

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
