using System.Text.RegularExpressions;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.Inventario.Application.Reservas;
using Bastion.Inventario.Contracts.Existencias;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Inventario.Infrastructure.Persistencia.Reservas;
using Bastion.Pruebas.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Reservas;

/// <summary>
/// Las tres sentencias crudas de la reserva —el cerrojo, lo reservado de las claves y el
/// disponible— nombran las tablas del modelo, comparan la empresa ellas mismas con el valor del
/// inquilino, y cuentan como activa lo mismo que el dominio.
/// </summary>
/// <remarks>
/// <para>
/// <b>El mismo trato que el cerrojo del recuento</b>
/// (<c>LaSentenciaDelRecuentoNombraLaTablaYLaEmpresaTests</c>): la cláusula de bloqueo y la suma
/// con caducidad no tienen traductor en EF Core, así que van escritas a mano, el filtro global no
/// las alcanza y una cadena no se entera de un renombrado.
/// </para>
/// <para>
/// <b>La empresa la distingue un caso por la API</b>: en <c>LasReservasTests</c>, otra empresa lee
/// ceros con los identificadores de esta. Ese caso necesita Docker; este lee las cadenas en el
/// carril rápido, y dice qué parte de qué sentencia se ha movido.
/// </para>
/// </remarks>
public sealed class LasSentenciasDeLaReservaNombranLaTablaYLaEmpresaTests : IDisposable
{
    private const string Fuentes =
        "src/Modules/Inventario/Bastion.Inventario.Infrastructure/Persistencia/";

    private static readonly (string Nombre, string Sql)[] s_sentencias =
    [
        (nameof(RepositorioDeReservas.SqlDelCerrojo), RepositorioDeReservas.SqlDelCerrojo),
        (nameof(LoReservado.SqlDeLasClaves), LoReservado.SqlDeLasClaves),
        (nameof(LoReservado.SqlDelDisponible), LoReservado.SqlDelDisponible),
    ];

    private readonly ApiSinDependencias _api = new();

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    /// <summary>Cada tabla que nombran, con su alias, es la de la entidad del modelo.</summary>
    [Fact]
    public void Cada_tabla_que_nombran_es_la_del_modelo()
    {
        List<string> encontradas = [.. s_sentencias
            .SelectMany(sentencia => Regex.Matches(
                sentencia.Sql,
                @"\b(?:FROM|JOIN) ([a-z_]+\.[a-z_]+) AS ([a-z])\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(1)))
            .Select(coincidencia => $"{coincidencia.Groups[1].Value} AS {coincidencia.Groups[2].Value}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        // El barrido se afirma a sí mismo antes de mirar nada (ADR-0020).
        encontradas.ShouldBe(
        [
            "inventario.consumos_de_reserva AS x",
            "inventario.reservas AS r",
            "inventario.valoraciones AS v",
        ]);

        foreach ((string alias, Type tipo) in s_alias)
        {
            IEntityType entidad = Entidad(tipo);

            encontradas.ShouldContain($"{entidad.GetSchema()}.{entidad.GetTableName()} AS {alias}", tipo.Name);
        }
    }

    /// <summary>Cada columna que nombran por su alias existe en la tabla de ese alias.</summary>
    [Fact]
    public void Cada_columna_que_nombran_existe_en_su_tabla()
    {
        Dictionary<string, string[]> esperadas = new(StringComparer.Ordinal)
        {
            ["r"] = ["almacen_id", "articulo_id", "caduca_el", "cantidad", "empresa_id", "estado", "id"],
            ["v"] = ["almacen_id", "articulo_id", "cantidad", "empresa_id"],
            ["x"] = ["cantidad", "reserva_id"],
        };

        foreach ((string alias, Type tipo) in s_alias)
        {
            List<string> encontradas = [.. s_sentencias
                .SelectMany(sentencia => Regex.Matches(
                    sentencia.Sql,
                    $@"\b{alias}\.([a-z_]+)\b",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(1)))
                .Select(coincidencia => coincidencia.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];

            // Primero que el barrido de este alias ha encontrado lo que debía.
            encontradas.ShouldBe(
                esperadas[alias], ignoreOrder: false, customMessage: $"las columnas del alias {alias}");

            IEntityType entidad = Entidad(tipo);
            List<string> mapeadas = [.. entidad.GetProperties().Select(propiedad => Columna(entidad, propiedad.Name))];

            encontradas.Where(nombrada => !mapeadas.Contains(nombrada, StringComparer.Ordinal))
                .ShouldBeEmpty($"columnas de {tipo.Name} que el modelo no mapea");
        }
    }

    /// <summary>
    /// El cerrojo es el del <c>UPDATE</c>, el mismo que el de una salida sobre la fila de la
    /// valoración, y no crea la fila: solo la lee.
    /// </summary>
    [Fact]
    public void El_cerrojo_es_el_del_UPDATE_y_no_crea_la_fila()
    {
        string cerrojo = RepositorioDeReservas.SqlDelCerrojo;

        // `FOR UPDATE` también chocaría con el `FOR KEY SHARE` de quien solo mira que la fila existe;
        // `FOR SHARE` no chocaría consigo mismo, y dos reservas leerían el mismo disponible.
        cerrojo.ShouldEndWith(" FOR NO KEY UPDATE");
        cerrojo.ShouldNotContain("FOR UPDATE");
        cerrojo.ShouldNotContain("FOR SHARE");
        cerrojo.ShouldStartWith("SELECT ");
        cerrojo.ShouldNotContain("INSERT");
    }

    /// <summary>Ninguna lleva punto y coma final.</summary>
    [Fact]
    public void Ninguna_lleva_punto_y_coma()
    {
        // EF Core las envuelve en un `SELECT ... FROM (...)`, y ahí dentro un punto y coma es un
        // 42601 en ejecución (ADR-0043).
        foreach ((string nombre, string sql) in s_sentencias)
        {
            sql.ShouldNotContain(";", customMessage: nombre);
        }
    }

    /// <summary>
    /// Cada tabla de cada sentencia compara la empresa contra el primer parámetro, y el cerrojo, la
    /// clave contra los dos siguientes.
    /// </summary>
    [Fact]
    public void Cada_tabla_compara_la_empresa_contra_el_primer_parametro()
    {
        Posicion(RepositorioDeReservas.SqlDelCerrojo, "v", "empresa_id").ShouldBe("0");
        Posicion(RepositorioDeReservas.SqlDelCerrojo, "v", "articulo_id").ShouldBe("1");
        Posicion(RepositorioDeReservas.SqlDelCerrojo, "v", "almacen_id").ShouldBe("2");

        Posicion(LoReservado.SqlDeLasClaves, "r", "empresa_id").ShouldBe("0");

        Posicion(LoReservado.SqlDelDisponible, "r", "empresa_id").ShouldBe("0");
        Regex.IsMatch(
                LoReservado.SqlDelDisponible,
                @"\bON v\.empresa_id = \{0\}",
                RegexOptions.None,
                TimeSpan.FromSeconds(1))
            .ShouldBeTrue(LoReservado.SqlDelDisponible);
    }

    /// <summary>
    /// Lo que cuentan como reservado es lo que el dominio cuenta como activo: el estado guardado
    /// como lo escribe el modelo, y la caducidad estrictamente posterior a ahora.
    /// </summary>
    [Fact]
    public void Lo_que_cuentan_como_activo_es_lo_que_el_dominio_cuenta()
    {
        IEntityType reserva = Entidad(typeof(Reserva));
        object? activa = reserva.FindProperty(ConfiguracionDeReserva.Estado)!
            .GetTypeMapping()
            .Converter!
            .ConvertToProvider(EstadoDeReserva.Activa);

        // Primero que el modelo lo guarda como texto: un número no casaría con ningún literal.
        activa.ShouldBe(nameof(EstadoDeReserva.Activa));

        foreach (string sql in new[] { LoReservado.SqlDeLasClaves, LoReservado.SqlDelDisponible })
        {
            sql.ShouldContain($"r.estado = '{activa}'");

            // Activa mientras la caducidad sea posterior a ahora: en el instante mismo ya no aparta,
            // como `Reserva.EstadoEn` (ADR-0059 §4).
            sql.ShouldContain("(r.caduca_el IS NULL OR r.caduca_el > {1})");
        }
    }

    /// <summary>Ningún puerto deja que quien llama elija la empresa.</summary>
    [Fact]
    public void Los_puertos_no_dejan_que_quien_llama_elija_la_empresa()
    {
        Parametros(typeof(IConsultaDeExistencias), nameof(IConsultaDeExistencias.DisponibleDeAsync))
            .ShouldBe(["almacenId", "articulos", "cancelacion"]);
        Parametros(typeof(IRepositorioDeReservas), nameof(IRepositorioDeReservas.BloquearLaValoracionAsync))
            .ShouldBe(["clave", "cancelacion"]);
        Parametros(typeof(IRepositorioDeReservas), nameof(IRepositorioDeReservas.ReservadoDeAsync))
            .ShouldBe(["claves", "ahora", "cancelacion"]);
    }

    /// <summary>
    /// El valor que comparan sale del inquilino y va en su sitio, y no hay otra sentencia cruda en
    /// esos ficheros.
    /// </summary>
    [Fact]
    public void El_valor_que_comparan_sale_del_inquilino_y_va_en_su_sitio()
    {
        // Qué va en la posición `{0}` es una línea de C#, y se lee del fuente.
        string repositorio = Fuente("Repositorios/RepositorioDeReservas.cs");
        string disponible = Fuente("Reservas/ElDisponibleDeLasExistencias.cs");

        // Primero cuántas hay: una tercera sin comprobar pasaría por las de abajo sin que nada lo
        // notara.
        Regex.Count(repositorio, @"\.SqlQueryRaw<").ShouldBe(2);
        Regex.Count(disponible, @"\.SqlQueryRaw<").ShouldBe(1);

        repositorio.ShouldContain("EmpresaDelInquilino() => inquilino.EmpresaDelFiltro ??");
        repositorio.ShouldContain(
            ".SqlQueryRaw<decimal>(SqlDelCerrojo, EmpresaDelInquilino(), clave.ArticuloId, clave.AlmacenId)");
        repositorio.ShouldContain(
            ".SqlQueryRaw<FilaDeLoReservado>(LoReservado.SqlDeLasClaves, EmpresaDelInquilino(), ahora, articulos, almacenes)");

        disponible.ShouldContain("Guid empresaId = inquilino.EmpresaDelFiltro ??");
        disponible.ShouldContain(
            "LoReservado.SqlDelDisponible, empresaId, reloj.GetUtcNow(), almacenId, articulos.ToArray())");
    }

    private static readonly (string Alias, Type Tipo)[] s_alias =
    [
        ("r", typeof(Reserva)),
        ("v", typeof(Valoracion)),
        ("x", typeof(ConsumoDeReserva)),
    ];

    private static string Posicion(string sql, string alias, string columna)
    {
        Match condicion = Regex.Match(
            sql,
            $@"\b(?:WHERE|AND) {alias}\.{Regex.Escape(columna)} = \{{(\d+)\}}",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        return condicion.Success ? condicion.Groups[1].Value : "(ninguna)";
    }

    private static string[] Parametros(Type puerto, string metodo) =>
        [.. puerto.GetMethod(metodo)!.GetParameters().Select(parametro => parametro.Name!)];

    private static string Fuente(string relativa) =>
        File.ReadAllText(Path.Combine(RaizDelRepositorio.Ruta(), Fuentes + relativa));

    private IEntityType Entidad(Type tipo)
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        return alcance.ServiceProvider.GetRequiredService<InventarioDbContext>().Model.FindEntityType(tipo)
            ?? throw new InvalidOperationException($"{tipo.Name} no está en el modelo.");
    }

    private static string Columna(IEntityType entidad, string propiedad) =>
        entidad.FindProperty(propiedad)!.GetColumnName(
            StoreObjectIdentifier.Create(entidad, StoreObjectType.Table)!.Value)!;
}
