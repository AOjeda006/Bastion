using System.Globalization;
using System.Runtime.CompilerServices;
using Bastion.Organizacion.Domain.Impuestos;
using Bastion.Organizacion.Domain.Unidades;
using Bastion.Organizacion.Infrastructure.Semillas;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Semillas;

/// <summary>
/// Las semillas del §12 llegan al directorio desde el que se cargan, y lo que traen dentro es
/// cargable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta es la mitad barata de la comprobación F3</b>, y mira la salida de la compilación: si
/// el <c>&lt;Content Include&gt;</c> de <c>Bastion.Organizacion.Infrastructure.csproj</c> deja de
/// copiarlas —o se le cae el <c>Link</c>, o alguien renombra la carpeta—, aquí se ve en la batería
/// rápida, sin Docker y en milisegundos. La otra mitad la hace el job <c>Humo</c> mirando DENTRO
/// de la imagen, porque la salida de <c>dotnet build</c> y lo que acaba en <c>/publicado</c> son
/// dos cosas distintas y solo la segunda es la que se despliega.
/// </para>
/// <para>
/// <b>Y se comprueba además que los datos son cargables sin base de datos.</b> Cada fila se
/// construye con la fábrica del dominio, que es la misma que usará el cargador: un porcentaje por
/// encima de cien, un código de doce caracteres o una cuenta del PGC con una letra revientan aquí
/// y no en el migrador de un despliegue. Sin esto, un error de tecleo en un <c>.json</c> es un
/// contenedor que sale con 1 en producción.
/// </para>
/// </remarks>
public sealed class LasSemillasLleganDondeSeCarganTests
{
    /// <summary>Carpeta del repositorio donde viven los ficheros, fuera de todo proyecto (§14).</summary>
    private const string EnElRepositorio = "db/semillas";

    [Fact]
    public void Las_semillas_estan_donde_el_cargador_las_busca()
    {
        // No se afirma sobre una ruta escrita a mano: se le pregunta al propio cargador dónde
        // mira. Si mañana cambia de sitio, este test le sigue en vez de quedarse verde mirando
        // una carpeta que ya no lee nadie.
        string carpeta = SemillasDeOrganizacion.CarpetaPublicada;

        Directory.Exists(carpeta).ShouldBeTrue(
            $"el cargador busca las semillas en «{carpeta}» y ahí no hay carpeta. Falta el " +
            "`<Content Include>` de Bastion.Organizacion.Infrastructure.csproj, o su `Link`");

        // La comprobación de verdad, en los dos sentidos, es la del propio cargador. Llamarla
        // aquí es lo que impide que este test y el migrador midan cosas distintas.
        Should.NotThrow(() => SemillasDeOrganizacion.ComprobarQueEstanTodas(carpeta));
    }

    [Fact]
    public void Las_del_repositorio_y_las_publicadas_son_las_mismas()
    {
        string[] enElRepositorio = NombresDeJson(Path.Combine(Raiz(), EnElRepositorio));
        string[] publicadas = NombresDeJson(SemillasDeOrganizacion.CarpetaPublicada);

        // En los dos sentidos, y con la lista del cargador de por medio. De menos: un fichero que
        // se versiona y no se publica es una semilla que nadie carga. De más: un fichero que se
        // publica y ya no está en el repositorio es una copia vieja que sobrevive en `bin/`.
        publicadas.ShouldBe(enElRepositorio);
        enElRepositorio.ShouldBe([.. SemillasDeOrganizacion.Ficheros.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void Los_impuestos_sembrados_son_impuestos_validos()
    {
        IReadOnlyList<FilaDeImpuesto> filas = LeerImpuestos();

        // La afirmación de conjunto no vacío. `Leer` ya la hace y lanza; se repite aquí porque lo
        // que este test dice es «hay tipos de IVA sembrados», y esa frase sin un número detrás la
        // cumple igual un fichero vacío.
        filas.ShouldNotBeEmpty();

        foreach (FilaDeImpuesto fila in filas)
        {
            Should.NotThrow(
                () => Impuesto.Crear(
                    fila.Codigo,
                    fila.Nombre,
                    fila.Tipo,
                    fila.Porcentaje,
                    fila.VigenteDesde,
                    fila.VigenteHasta,
                    fila.CuentaRepercutido,
                    fila.CuentaSoportado,
                    DateTimeOffset.UnixEpoch),
                $"la fila «{fila.Codigo}» de {SemillasDeOrganizacion.Impuestos} no es un impuesto válido");
        }

        // Un IVA general que se quede sin sembrar no rompe nada al arrancar: rompe la primera
        // factura, semanas después. Se nombran los tres tipos que una pyme española necesita sí o
        // sí, y se comprueba que cada uno tiene un tramo ABIERTO —el vigente hoy—, que es el que
        // se va a aplicar.
        foreach (string codigo in (string[])["IVA-GENERAL", "IVA-REDUCIDO", "IVA-SUPERREDUCIDO"])
        {
            filas.ShouldContain(
                fila => fila.Codigo == codigo && fila.VigenteHasta == null,
                $"{codigo} no tiene ningún tramo vigente en {SemillasDeOrganizacion.Impuestos}");
        }
    }

    [Fact]
    public void Ningun_impuesto_sembrado_pisa_a_otro_tramo_suyo()
    {
        // El solape lo prohíbe PostgreSQL con una restricción de exclusión, así que un fichero mal
        // editado no corrompe nada: revienta el migrador. Lo que hace este test es adelantar ese
        // rojo a la batería rápida, donde se lee el nombre de los dos tramos que se pisan en vez
        // de un `23P01` dentro de un contenedor.
        List<string> solapes =
        [
            .. from unos in LeerImpuestos()
               from otros in LeerImpuestos()
               where unos != otros
                   && string.Equals(unos.Codigo, otros.Codigo, StringComparison.Ordinal)
                   && unos.VigenteDesde <= (otros.VigenteHasta ?? DateOnly.MaxValue)
                   && otros.VigenteDesde <= (unos.VigenteHasta ?? DateOnly.MaxValue)
               select $"{unos.Codigo}: {unos.VigenteDesde} y {otros.VigenteDesde}",
        ];

        solapes.ShouldBeEmpty(string.Join("; ", solapes));
    }

    [Fact]
    public void Las_unidades_sembradas_son_unidades_validas()
    {
        IReadOnlyList<FilaDeUnidad> filas = SemillasDeOrganizacion.Leer<FilaDeUnidad>(
            SemillasDeOrganizacion.CarpetaPublicada, SemillasDeOrganizacion.UnidadesDeMedida);

        filas.ShouldNotBeEmpty();

        foreach (FilaDeUnidad fila in filas)
        {
            Should.NotThrow(
                () => UnidadMedida.Crear(fila.Codigo, fila.Nombre, fila.Decimales, DateTimeOffset.UnixEpoch),
                $"la fila «{fila.Codigo}» de {SemillasDeOrganizacion.UnidadesDeMedida} no es una unidad válida");
        }

        // El código es la identidad de la unidad y el cargador salta las repetidas: dos filas con
        // el mismo código serían una que nunca entra, en silencio.
        string[] codigos = [.. filas.Select(fila => UnidadMedida.NormalizarCodigo(fila.Codigo))];

        codigos.Distinct(StringComparer.Ordinal).Count().ShouldBe(
            codigos.Length, "hay unidades repetidas en " + SemillasDeOrganizacion.UnidadesDeMedida);

        // La unidad por omisión de cualquier alta. Sin ella, dar de alta el primer artículo no se
        // puede, y el síntoma aparece en el módulo de Inventario, que es de la fase 1.
        codigos.ShouldContain("UD");
    }

    [Fact]
    public void Sin_carpeta_no_se_da_por_cargado()
    {
        string inexistente = Path.Combine(Path.GetTempPath(), "bastion-semillas-" + Guid.NewGuid().ToString("N"));

        SemillasQueNoLleganException fallo = Should.Throw<SemillasQueNoLleganException>(
            () => SemillasDeOrganizacion.ComprobarQueEstanTodas(inexistente));

        // El mensaje es la mitad del valor: quien se lo encuentre en el registro de un contenedor
        // tiene que salir de ahí sabiendo dónde mirar.
        fallo.Message.ShouldContain("Content Include");
    }

    [Fact]
    public void Un_fichero_que_falta_se_dice_por_su_nombre()
    {
        using var carpeta = CarpetaDePruebas.Con(
            (SemillasDeOrganizacion.UnidadesDeMedida, "[{\"codigo\":\"UD\",\"nombre\":\"Unidad\",\"decimales\":0}]"));

        SemillasQueNoLleganException fallo = Should.Throw<SemillasQueNoLleganException>(
            () => SemillasDeOrganizacion.ComprobarQueEstanTodas(carpeta.Ruta));

        fallo.Message.ShouldContain(SemillasDeOrganizacion.Impuestos);
    }

    [Fact]
    public void Un_fichero_que_nadie_carga_tambien_se_dice()
    {
        using var carpeta = CarpetaDePruebas.Con(
            (SemillasDeOrganizacion.Impuestos, "[]"),
            (SemillasDeOrganizacion.UnidadesDeMedida, "[]"),
            ("paises.json", "[]"));

        SemillasQueNoLleganException fallo = Should.Throw<SemillasQueNoLleganException>(
            () => SemillasDeOrganizacion.ComprobarQueEstanTodas(carpeta.Ruta));

        // Sobrar es tan grave como faltar, y por eso el test existe: un `paises.json` publicado y
        // no leído es una semilla que alguien dio por sembrada. Que la lista sea de las que se
        // comprueban en los dos sentidos es lo que lo caza.
        fallo.Message.ShouldContain("paises.json");
    }

    [Fact]
    public void Un_fichero_vacio_no_pasa_por_cargado()
    {
        using var carpeta = CarpetaDePruebas.Con((SemillasDeOrganizacion.Impuestos, "[]"));

        // LA MUTACIÓN QUE DA SENTIDO A TODO ESTO. Un `[]` se interpreta sin ningún error, siembra
        // cero filas y sale con 0. Si esta línea no fuera roja, «cero impuestos» y «los impuestos
        // ya estaban» serían el mismo resultado observable.
        Should.Throw<SemillasQueNoLleganException>(
            () => SemillasDeOrganizacion.Leer<FilaDeImpuesto>(carpeta.Ruta, SemillasDeOrganizacion.Impuestos));
    }

    [Fact]
    public void Una_clave_con_una_errata_no_se_ignora()
    {
        using var carpeta = CarpetaDePruebas.Con(
            (SemillasDeOrganizacion.UnidadesDeMedida,
             "[{\"codigo\":\"UD\",\"nombre\":\"Unidad\",\"decimalez\":0}]"));

        // Por omisión, `System.Text.Json` se traga las claves que no casan. Con `decimalez` en vez
        // de `decimales`, la unidad entraría con cero decimales sin que nadie dijera nada — y cero
        // decimales es un valor legítimo, así que ni el dominio lo notaría. Lo que lo caza es
        // `JsonUnmappedMemberHandling.Disallow` en la fila, no la validación.
        SemillasQueNoLleganException fallo = Should.Throw<SemillasQueNoLleganException>(
            () => SemillasDeOrganizacion.Leer<FilaDeUnidad>(
                carpeta.Ruta, SemillasDeOrganizacion.UnidadesDeMedida));

        fallo.Message.ShouldContain("decimalez");
    }

    [Fact]
    public void Un_campo_que_falta_no_se_rellena_solo()
    {
        using var carpeta = CarpetaDePruebas.Con(
            (SemillasDeOrganizacion.UnidadesDeMedida, "[{\"codigo\":\"UD\",\"nombre\":\"Unidad\"}]"));

        // `decimales` es `required`: omitirlo no da cero, da excepción. Es lo que impide que una
        // unidad se siembre con una precisión que nadie eligió — y los decimales no se pueden
        // bajar después.
        Should.Throw<SemillasQueNoLleganException>(
            () => SemillasDeOrganizacion.Leer<FilaDeUnidad>(
                carpeta.Ruta, SemillasDeOrganizacion.UnidadesDeMedida));
    }

    [Fact]
    public void Los_comentarios_del_fichero_no_estorban()
    {
        using var carpeta = CarpetaDePruebas.Con(
            (SemillasDeOrganizacion.UnidadesDeMedida,
             "// por qué esta unidad\n[{\"codigo\":\"UD\",\"nombre\":\"Unidad\",\"decimales\":0}]"));

        // Los `.json` de verdad empiezan por veinte líneas de comentario explicando de dónde sale
        // cada número. Si esto dejara de leerse, el migrador se caería con «no se puede
        // interpretar» y nadie sabría por qué.
        SemillasDeOrganizacion
            .Leer<FilaDeUnidad>(carpeta.Ruta, SemillasDeOrganizacion.UnidadesDeMedida)
            .Count
            .ShouldBe(1);
    }

    private static IReadOnlyList<FilaDeImpuesto> LeerImpuestos() =>
        SemillasDeOrganizacion.Leer<FilaDeImpuesto>(
            SemillasDeOrganizacion.CarpetaPublicada, SemillasDeOrganizacion.Impuestos);

    private static string[] NombresDeJson(string carpeta) =>
    [
        .. Directory.EnumerateFiles(carpeta, SemillasDeOrganizacion.Extension)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal),
    ];

    // Misma cautela que en el resto de barridos: se parte del directorio del ensamblado, y el
    // fichero del test queda de segundo intento porque en la CI las rutas de los fuentes se
    // reescriben. Si no aparece por ninguno de los dos, REVIENTA.
    private static string Raiz([CallerFilePath] string desde = "")
    {
        string? raiz = Subiendo(AppContext.BaseDirectory) ?? Subiendo(Path.GetDirectoryName(desde));

        raiz.ShouldNotBeNull(
            "no se ha encontrado Bastion.sln, ni subiendo desde el ensamblado ni desde el fichero del test");

        return raiz;
    }

    private static string? Subiendo(string? partida)
    {
        DirectoryInfo? carpeta = string.IsNullOrEmpty(partida) ? null : new DirectoryInfo(partida);

        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "Bastion.sln")))
        {
            carpeta = carpeta.Parent;
        }

        return carpeta?.FullName;
    }

    /// <summary>Una carpeta temporal con el contenido que pida el test, y que se borra sola.</summary>
    private sealed class CarpetaDePruebas : IDisposable
    {
        private CarpetaDePruebas(string ruta) => Ruta = ruta;

        public string Ruta { get; }

        public static CarpetaDePruebas Con(params (string Fichero, string Contenido)[] ficheros)
        {
            string ruta = Path.Combine(
                Path.GetTempPath(),
                "bastion-semillas-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

            Directory.CreateDirectory(ruta);

            foreach ((string fichero, string contenido) in ficheros)
            {
                File.WriteAllText(Path.Combine(ruta, fichero), contenido);
            }

            return new CarpetaDePruebas(ruta);
        }

        public void Dispose() => Directory.Delete(Ruta, recursive: true);
    }
}
