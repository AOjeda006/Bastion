using System.Globalization;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application;
using Bastion.Organizacion.Application.Unidades;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Domain.Unidades;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Unidades;

/// <summary>
/// La desigualdad de la inversa y la negativa a encadenar, ejercidas <b>sin base de datos</b>
/// (ADR-0023, decisiones 2 y 3).
/// </summary>
/// <remarks>
/// <para>
/// <b>No lleva <c>Category=Integracion</c>, y es la segunda excepción declarada de este proyecto</b>
/// —la primera es <c>LaTraduccionASqlTests</c>, del ítem 1.3—. El motivo es el mismo y está escrito
/// en el <c>.csproj</c>: esto es «la parte del carril que no necesita el carril». Una desigualdad
/// entre dos <c>decimal</c> es aritmética, y decidir si se compone una cadena es una rama; ni lo
/// uno ni lo otro abre una conexión.
/// </para>
/// <para>
/// <b>Y que no la necesite es justo por lo que no puede estar detrás de Docker.</b> Hasta este
/// fichero, el único testigo del <c>≤</c> vivía en <c>Api.IntegrationTests</c>. Con Docker parado
/// —que es como está la máquina en la que se escribe el cambio— cambiar el <c>≤</c> por un
/// <c>&lt;</c> compila, sale verde, y el aviso llega en el <i>runner</i>, minutos después y en otro
/// sitio. Un caso frontera al que solo se puede llegar por un contenedor es un caso frontera que
/// nadie ejerce mientras lo escribe.
/// </para>
/// <para>
/// <b>Esto no sustituye a los casos por HTTP</b>, que siguen en <c>Api.IntegrationTests</c>: allí se
/// comprueba que el rechazo llega al cliente como un <c>409</c> con su <c>type</c>, y que el
/// resolutor contesta <c>404</c>. Aquí se comprueba el número. Son dos afirmaciones distintas y
/// ninguna implica a la otra.
/// </para>
/// </remarks>
public sealed class LaAritmeticaDeLaInversaTests
{
    private static readonly Guid s_kg = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid s_g = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid s_mg = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>El margen no está escrito: sale de los decimales del factor.</summary>
    /// <remarks>
    /// El arnés del arnés. Todo lo de abajo compara contra <c>MargenPorFactor</c>, así que si ese
    /// número dejara de valer <c>5·10⁻⁷</c> las comparaciones seguirían cuadrando entre ellas y la
    /// tolerancia pasaría a ser otra sin que nada se pusiera rojo. Esta es la única aserción que
    /// mira el número contra la escala de la que tiene que salir.
    /// </remarks>
    [Fact]
    public void El_margen_sale_de_la_escala_y_no_de_un_numero_elegido()
    {
        ConversionUM.DecimalesDelFactor.ShouldBe(
            6, "el ADR-0023 cita seis decimales, y de ahí sale todo lo demás");

        LaInversaEsPlausible.MargenPorFactor.ShouldBe(
            0.0000005m,
            "media unidad del último decimal de la escala, que es el error máximo que un redondeo " +
            "a seis decimales puede haber metido en cada factor");
    }

    /// <summary>La desigualdad del ADR, en los casos que la definen.</summary>
    /// <remarks>
    /// <para>
    /// <b>El caso de <c>0,083334</c> se RECHAZA, y el enunciado del ítem lo daba por aceptado.</b>
    /// La desigualdad está fijada y no se toca, así que manda ella: con <c>f = 12</c>,
    /// <c>|12 × 0,083334 − 1| = 8·10⁻⁶</c> y el margen es <c>5·10⁻⁷ × 12,083334 ≈ 6,04·10⁻⁶</c>, que
    /// es menor. Y el rechazo es lo correcto: <c>1/12 = 0,08333333…</c>, así que <c>0,083334</c> se
    /// separa <c>6,67·10⁻⁷</c> del valor real —más de media unidad del último decimal— y por tanto
    /// <b>no es</b> un redondeo de <c>1/12</c> a seis decimales; <c>0,083333</c>, a
    /// <c>3,33·10⁻⁷</c>, sí lo es. Ensanchar la tolerancia para que entrara sería exactamente el
    /// «número elegido por comodidad» que el ADR prohíbe.
    /// </para>
    /// <para>
    /// El par de <c>1,001</c> y <c>0,999</c> es el caso frontera: se separan <b>exactamente</b> el
    /// margen. Lo verifica el caso de abajo, porque de nada sirve un frontera que no esté en la
    /// frontera.
    /// </para>
    /// </remarks>
    /// <param name="factor">El factor directo, como texto para que no lo redondee el compilador.</param>
    /// <param name="inverso">El factor del sentido contrario.</param>
    /// <param name="casan">Si el par pasa la desigualdad.</param>
    [Theory]
    [InlineData("12", "0.083333", true)]
    [InlineData("12", "0.083334", false)]
    [InlineData("12", "0.5", false)]
    [InlineData("1.001", "0.999", true)]
    [InlineData("1.001", "0.998999", false)]
    public void La_desigualdad_acepta_el_redondeo_legitimo_y_rechaza_el_otro_numero(
        string factor, string inverso, bool casan) =>
        LaInversaEsPlausible.Casan(ADecimal(factor), ADecimal(inverso)).ShouldBe(
            casan,
            $"{factor} × {inverso} = {ADecimal(factor) * ADecimal(inverso)}, que se separa de 1 en " +
            $"{Math.Abs((ADecimal(factor) * ADecimal(inverso)) - 1m)}, contra un margen de " +
            $"{LaInversaEsPlausible.MargenPorFactor * (ADecimal(factor) + ADecimal(inverso))}");

    /// <summary>Y el caso frontera está en la frontera, clavado.</summary>
    /// <remarks>
    /// <b>Sin esto, el <c>≤</c> no está vigilado.</b> El caso de arriba dice que <c>1,001</c> y
    /// <c>0,999</c> se aceptan; si estuvieran <i>dentro</i> del margen en vez de <i>en</i> el
    /// margen, cambiar el <c>≤</c> por un <c>&lt;</c> los seguiría aceptando y la mutación pasaría
    /// en verde. Esta igualdad es lo único que convierte aquel caso en el que separa los dos
    /// signos.
    /// </remarks>
    [Fact]
    public void El_par_de_la_frontera_se_separa_EXACTAMENTE_el_margen()
    {
        const decimal Factor = 1.001m;
        const decimal Inverso = 0.999m;

        decimal separacion = Math.Abs((Factor * Inverso) - 1m);
        decimal margen = LaInversaEsPlausible.MargenPorFactor * (Factor + Inverso);

        separacion.ShouldBe(
            margen,
            "este par tiene que estar EN la frontera y no dentro: es el único sitio en el que un " +
            "`<` y un `≤` contestan cosas distintas, y por tanto el único desde el que se puede " +
            "afirmar cuál de los dos está escrito");

        LaInversaEsPlausible.Casan(Factor, Inverso).ShouldBeTrue(
            "el margen es el error MÁXIMO que la escala puede producir, así que separarse " +
            "exactamente eso es un redondeo legítimo y no una discrepancia: por eso es `≤`");
    }

    /// <summary>Sin fila inversa no hay nada que contradecir, y la regla se calla.</summary>
    [Fact]
    public void Sin_la_fila_del_sentido_contrario_no_hay_nada_que_comprobar() =>
        LaInversaEsPlausible.Comprobar(inversa: null, factor: 12m).EsCorrecto.ShouldBeTrue(
            "el ADR concede declarar un solo sentido; lo que acota es la relación entre los dos " +
            "cuando existen los dos");

    /// <summary>El rechazo es un error de negocio con nombre, no una excepción.</summary>
    [Fact]
    public void La_inversa_implausible_se_rechaza_con_su_codigo_y_no_reventando()
    {
        ConversionUM inversa = Conversion(s_g, s_kg, 0.5m);

        Resultado resultado = LaInversaEsPlausible.Comprobar(inversa, factor: 12m);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("conversion-um-inversa-implausible");
        resultado.Error.Mensaje.ShouldContain(
            "0.5", customMessage: "el mensaje dice con qué factor choca, que es lo que hace falta " +
            "para corregirlo");
    }

    /// <summary>
    /// <c>kg→g</c> y <c>g→mg</c> declaradas, <c>kg→mg</c> preguntado: error con nombre, no 1000000.
    /// </summary>
    /// <remarks>
    /// El caso que importa, y el único en el que las tres salidas malas —componer, cero, nulo— se
    /// distinguen de la buena. Un resolutor que devolviera nulo o cero pasaría cualquier prueba que
    /// solo mirase el par declarado.
    /// </remarks>
    [Fact]
    public async Task El_par_que_habria_que_encadenar_no_se_resuelve()
    {
        var repositorio = new ConversionesDeMentira(
            Conversion(s_kg, s_g, 1000m), Conversion(s_g, s_mg, 1000m));

        var resolutor = new ResolverConversionUm(repositorio);

        Resultado<ResolucionDeConversionDto> resuelto =
            await resolutor.EjecutarAsync(s_kg, s_mg, CancellationToken.None);

        resuelto.EsCorrecto.ShouldBeFalse(
            "kg→mg no está declarada: componerla multiplicando kg→g por g→mg daría 1000000, un " +
            "número que nadie ha declarado y que arrastra el redondeo de los dos saltos");

        resuelto.Error!.Codigo.ShouldBe(
            "conversion-um-no-declarada",
            "y falla con NOMBRE: un nulo se propaga sin ruido y un cero convierte las existencias " +
            "en nada, y las dos cosas llegan al inventario sin un solo error por el camino");

        resuelto.Error.Mensaje.Contains("1000000", StringComparison.Ordinal).ShouldBeFalse(
            "ni siquiera de adorno: es justo el número que no existe");

        repositorio.ParesPedidos.ShouldBe(
            [(s_kg, s_mg)],
            "una sola lectura, del par exacto. Un segundo intento por el sentido contrario o una " +
            "búsqueda de caminos serían componer una conversión que nadie declaró");
    }

    /// <summary>Y el par declarado sí se resuelve, para que lo de arriba no sea un resolutor roto.</summary>
    [Fact]
    public async Task El_par_declarado_se_resuelve_y_dice_si_esta_retirada()
    {
        ConversionUM kilo = Conversion(s_kg, s_g, 1000m);
        kilo.Retirar();

        var resolutor = new ResolverConversionUm(new ConversionesDeMentira(kilo));

        Resultado<ResolucionDeConversionDto> resuelto =
            await resolutor.EjecutarAsync(s_kg, s_g, CancellationToken.None);

        resuelto.EsCorrecto.ShouldBeTrue(
            "«no se ofrece para operaciones nuevas, pero sigue resolviendo para lo que ya apunta " +
            "a ella»: resolver es exactamente lo que sigue haciendo");

        resuelto.Valor.Factor.ShouldBe(1000m);
        resuelto.Valor.Retirada.ShouldBeTrue("y quien resuelve por una fila retirada se entera");
    }

    /// <summary>La MODIFICACIÓN vuelve a comprobar la inversa, no solo el alta.</summary>
    /// <remarks>
    /// El camino por el que se rompe, entero: se da de alta <c>A→B</c>, luego <c>B→A</c>, y después
    /// se modifica la primera. Una comprobación que solo mirase el alta deja pasar exactamente
    /// esto, y lo deja pasar en verde.
    /// </remarks>
    [Fact]
    public async Task La_modificacion_vuelve_a_comprobar_la_inversa()
    {
        ConversionUM directa = Conversion(s_kg, s_g, 1000m);
        ConversionUM inversa = Conversion(s_g, s_kg, 0.001m);

        var repositorio = new ConversionesDeMentira(directa, inversa);
        var unidadTrabajo = new UnidadDeTrabajoDeMentira();

        var modificar = new ModificarConversionUm(
            repositorio, unidadTrabajo, new VersionesDeMentira());

        Resultado<ConversionUmDto> resultado = await modificar.EjecutarAsync(
            directa.Id,
            new VersionDeRecurso(1),
            new ModificarConversionUmDto { Factor = 2m },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse(
            "con g→kg declarada a 0,001, cambiar kg→g a 2 deja el par contradiciéndose: el mismo " +
            "inventario cuadraría por un lado y descuadraría por el otro");

        resultado.Error!.Codigo.ShouldBe("conversion-um-inversa-implausible");

        directa.Factor.ShouldBe(1000m, "y no ha llegado a tocar la fila");
        unidadTrabajo.Confirmaciones.ShouldBe(0, "ni a confirmar nada");
    }

    /// <summary>Y una modificación coherente sí pasa, para que lo de arriba no sea un «no» fijo.</summary>
    [Fact]
    public async Task Una_modificacion_que_respeta_la_inversa_se_guarda()
    {
        ConversionUM directa = Conversion(s_kg, s_g, 1000m);
        ConversionUM inversa = Conversion(s_g, s_kg, 0.001m);

        var repositorio = new ConversionesDeMentira(directa, inversa);
        var unidadTrabajo = new UnidadDeTrabajoDeMentira();

        var modificar = new ModificarConversionUm(
            repositorio, unidadTrabajo, new VersionesDeMentira());

        Resultado<ConversionUmDto> resultado = await modificar.EjecutarAsync(
            directa.Id,
            new VersionDeRecurso(1),
            new ModificarConversionUmDto { Factor = 1000.0001m },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue(
            "1000,0001 × 0,001 = 1,0000001, que se separa de 1 en 10⁻⁷, y el margen es " +
            $"{LaInversaEsPlausible.MargenPorFactor * 1000.0011m}. " +
            (resultado.EsCorrecto ? string.Empty : resultado.Error!.Mensaje));

        directa.Factor.ShouldBe(1000.0001m);
        unidadTrabajo.Confirmaciones.ShouldBe(1);
    }

    private static decimal ADecimal(string numero) =>
        decimal.Parse(numero, CultureInfo.InvariantCulture);

    private static ConversionUM Conversion(Guid origen, Guid destino, decimal factor) =>
        ConversionUM.Crear(origen, destino, factor, DateTimeOffset.UnixEpoch);

    /// <summary>Las conversiones que hay, sin base: lo que se prueba aquí no es la consulta.</summary>
    /// <remarks>
    /// Apunta los pares que le piden, porque «una sola lectura, del par exacto» es una afirmación
    /// sobre lo que el resolutor <b>hace</b>, y sin esto no habría manera de distinguir un
    /// resolutor que no encadena de uno que lo intentó y no encontró el camino.
    /// </remarks>
    private sealed class ConversionesDeMentira(params ConversionUM[] declaradas)
        : IRepositorioDeConversiones
    {
        public List<(Guid Origen, Guid Destino)> ParesPedidos { get; } = [];

        public IReadOnlySet<string> CamposOrdenables => new HashSet<string>(StringComparer.Ordinal);

        public Task<ConversionUM?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
            Task.FromResult(declaradas.FirstOrDefault(fila => fila.Id == id));

        public Task<bool> ExisteAsync(Guid origen, Guid destino, CancellationToken cancelacion) =>
            Task.FromResult(declaradas.Any(fila =>
                fila.UnidadOrigenId == origen && fila.UnidadDestinoId == destino));

        public Task<ConversionUM?> DelParAsync(
            Guid origen, Guid destino, CancellationToken cancelacion)
        {
            ParesPedidos.Add((origen, destino));

            return Task.FromResult(declaradas.FirstOrDefault(fila =>
                fila.UnidadOrigenId == origen && fila.UnidadDestinoId == destino));
        }

        public Task<PaginaDe<ConversionUM>> ListarAsync(
            Paginacion paginacion, CancellationToken cancelacion) =>
            throw new NotSupportedException("Este doble no lista: aquí no se prueba el listado.");

        public void Agregar(ConversionUM conversion) =>
            throw new NotSupportedException("Este doble no da de alta: aquí no se prueba el alta.");
    }

    private sealed class UnidadDeTrabajoDeMentira : IUnidadTrabajoDeOrganizacion
    {
        public int Confirmaciones { get; private set; }

        public Task<int> ConfirmarAsync(CancellationToken cancelacion)
        {
            Confirmaciones++;

            return Task.FromResult(1);
        }
    }

    // `Exigir` no hace nada porque el 412 lo decide el motor al guardar, y aquí no se guarda: lo
    // que se comprueba es qué pasa ANTES, cuando la inversa no casa.
    private sealed class VersionesDeMentira : IVersionesDeOrganizacion
    {
        public VersionDeRecurso De(object entidad) => new(1);

        public void Exigir(object entidad, VersionDeRecurso version)
        {
        }
    }
}
