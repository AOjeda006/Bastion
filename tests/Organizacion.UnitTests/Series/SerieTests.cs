using System.Reflection;
using Bastion.Organizacion.Domain.Series;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Series;

/// <summary>
/// Lo que una serie garantiza por sí sola, que desde el ítem 2.4 <b>ya no incluye numerar</b>.
/// </summary>
/// <remarks>
/// <para>
/// Este fichero tenía cuatro casos alrededor de <c>Serie.RegistrarNumeroAsignado</c>: que el
/// contador avanzara de uno en uno, que una serie cerrada no admitiera más números, que subirlo
/// impidiera suprimir la serie y que el contador fuera una columna y no una secuencia. El método
/// <b>se borró</b> —ningún módulo podía llamarlo, porque <c>Serie</c> vive en
/// <c>Organizacion.Domain</c>— y con él se fueron sus dos guardas, que ahora viajan en el
/// <c>WHERE</c> de la sentencia que numera.
/// </para>
/// <para>
/// <b>Los casos no se han perdido, han cambiado de sitio</b>, y decir dónde es parte del cambio: el
/// avance de uno en uno y la serie cerrada se comprueban contra PostgreSQL de verdad, porque la
/// invariante ya es del motor; y suprimir una serie que ha numerado sigue siendo un <c>409</c> en
/// <c>ContratoDeOrganizacionTests</c>. Aquí se queda lo que el dominio sí sostiene solo, más lo que
/// el cambio estrena: que <b>no hay por dónde</b> subir el contador desde C#.
/// </para>
/// </remarks>
public sealed class SerieTests
{
    private static readonly Guid s_empresa = Guid.Parse("2f6d5f4e-0000-4000-8000-000000000001");
    private static readonly Guid s_ejercicio = Guid.Parse("2f6d5f4e-0000-4000-8000-000000000002");
    private static readonly DateTimeOffset s_momento = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    private static Serie Nueva(string codigo = "FV") => Serie.Crear(
        s_empresa, s_ejercicio, TipoDeDocumento.FacturaEmitida, codigo, "{serie}/{anio}/{numero:0000}", s_momento);

    [Fact]
    public void Una_serie_nace_activa_y_con_su_fila_de_contador_a_cero()
    {
        Serie serie = Nueva();

        serie.Estado.ShouldBe(EstadoDeSerie.Activa);
        serie.EmpresaId.ShouldBe(s_empresa);
        serie.EjercicioId.ShouldBe(s_ejercicio);

        // LA FILA EXISTE DESDE EL PRIMER INSTANTE, y no aparece con la primera numeración. De eso
        // depende que «ninguna fila devuelta» sea un fallo sin ambigüedad cuando se numere: si la
        // fila se creara al numerar por primera vez, la sentencia no podría distinguir «esta serie
        // está cerrada o es de otra empresa» de «todavía no tiene contador».
        serie.Numeracion.ShouldNotBeNull();
        serie.Contador.ShouldBe(0);
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
    public void Cerrar_una_serie_conserva_su_fila_de_contador()
    {
        // Cerrar NO es lo que impide numerar —eso lo hace el `WHERE` de la sentencia, que exige
        // el estado activo—: lo que se comprueba aquí es que cerrar no se lleva por delante el
        // contador. Una serie cerrada tiene que seguir diciendo por cuánto iba, porque es lo que
        // demuestra la correlatividad de los documentos que ya emitió.
        Serie serie = Nueva();

        serie.Cerrar();

        serie.Estado.ShouldBe(EstadoDeSerie.Cerrada);
        serie.Numeracion.ShouldNotBeNull();
        serie.Contador.ShouldBe(0);
    }

    [Fact]
    public void Una_serie_cargada_sin_su_fila_de_contador_lanza_en_vez_de_contestar_cero()
    {
        // La fila hija se carga SIEMPRE con la serie —la configuración lo declara—, así que esta
        // situación no la produce ninguna consulta de hoy. El caso existe por lo que pasaría si
        // mañana alguien escribiera una que la dejara fuera: un cero silencioso aquí es
        // `SePuedeSuprimir` diciendo que sí sobre una serie que ya ha numerado, con un `DELETE`
        // detrás. De los dos modos de fallar —«borra lo que no debía» y «no contesta»— solo el
        // segundo se puede depurar, y este caso es el que elige cuál toca.
        var sinContador = (Serie)Activator.CreateInstance(typeof(Serie), nonPublic: true)!;

        sinContador.Numeracion.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => sinContador.Contador);
        Should.Throw<InvalidOperationException>(() => sinContador.SePuedeSuprimir);
    }

    [Fact]
    public void Nada_de_lo_que_se_ve_desde_fuera_de_ContadorDeSerie_sube_el_numero()
    {
        // ESTA AUSENCIA ES LA INVARIANTE, y sustituye —con ventaja— a lo que comprobaba
        // `RegistrarNumeroAsignado`. Aquel método era la última defensa contra un llamante que
        // pasara el número equivocado; aquí no hay número que un llamante pueda pasar, porque no
        // hay por dónde entrar: lo único que escribe esa columna es la sentencia del mecanismo,
        // que incrementa sobre lo que hay.
        //
        // La lista es CERRADA y se compara entera, no «que no haya setters»: un método nuevo que
        // subiera el contador tendría cualquier nombre, y una regla que buscara nombres no lo
        // vería. Si esto se pone rojo, la pregunta no es cómo ampliar la lista: es si la
        // invariante de R5 sigue viviendo en un solo sitio.
        List<string> visibles = [.. typeof(ContadorDeSerie)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(metodo => metodo.IsPublic || metodo.IsAssembly || metodo.IsFamilyOrAssembly)
            .Select(metodo => metodo.Name)];

        visibles.Sort(StringComparer.Ordinal);

        visibles.ShouldBe(["ParaSerieNueva", "get_SerieId", "get_UltimoNumero"]);
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
