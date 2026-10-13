using System.Text.RegularExpressions;
using Bastion.Api.FunctionalTests.Persistencia;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.Inventario.Application.Saldos;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;
using Bastion.Inventario.Infrastructure.Persistencia.Saldos;
using Bastion.Pruebas.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Saldos;

/// <summary>
/// La sentencia del saldo a una fecha nombra las tablas del modelo, compara la empresa ella misma
/// con el valor del inquilino en cada una, compara la fecha con su parámetro, y cuenta como borrador
/// lo que el modelo guarda como borrador.
/// </summary>
/// <remarks>
/// <para>
/// <b>El mismo trato que las de la reserva</b> (<c>LasSentenciasDeLaReservaNombranLaTablaYLaEmpresaTests</c>):
/// la instantánea, el libro desde el mes siguiente y el tránsito no tienen traductor en una sola
/// sentencia, así que va escrita a mano, el filtro global no la alcanza y una cadena no se entera de
/// un renombrado (ADR-0063 §2).
/// </para>
/// <para>
/// <b>Lo que suma lo afirman los casos por la API</b>, en <c>ElSaldoAUnaFechaTests</c>, contra el
/// libro y con Docker; este lee la cadena en el carril rápido, y dice qué parte se ha movido.
/// </para>
/// </remarks>
public sealed class LaSentenciaDelSaldoNombraLasTablasYLaEmpresaTests : IDisposable
{
    private const string Fuente =
        "src/Modules/Inventario/Bastion.Inventario.Infrastructure/Persistencia/Saldos/LosSaldosAUnaFecha.cs";

    private static readonly (string Alias, Type Tipo)[] s_alias =
    [
        ("c", typeof(CorteDeLaInstantanea)),
        ("e", typeof(Existencia)),
        ("i", typeof(InstantaneaMensual)),
        ("l", typeof(LineaDeTransferencia)),
        ("m", typeof(MovimientoStock)),
        ("n", typeof(Transferencia)),
        ("t", typeof(Transferencia)),
        ("v", typeof(Valoracion)),
    ];

    private readonly ApiSinDependencias _api = new();

    private static string Sql => LosSaldosAUnaFecha.SqlDelSaldo;

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    /// <summary>Cada tabla que nombra, con su alias, es la de la entidad del modelo.</summary>
    [Fact]
    public void Cada_tabla_que_nombra_es_la_del_modelo()
    {
        List<string> encontradas = [.. Regex.Matches(
                Sql,
                @"\b(?:FROM|JOIN) ([a-z_]+\.[a-z_]+) AS ([a-z])\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(1))
            .Select(coincidencia => $"{coincidencia.Groups[1].Value} AS {coincidencia.Groups[2].Value}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        // El barrido se afirma a sí mismo antes de mirar nada (ADR-0020).
        encontradas.ShouldBe(
        [
            "inventario.cortes_de_la_instantanea AS c",
            "inventario.existencias AS e",
            "inventario.instantaneas_mensuales AS i",
            "inventario.lineas_transferencia AS l",
            "inventario.movimiento_stock AS m",
            "inventario.transferencias AS n",
            "inventario.transferencias AS t",
            "inventario.valoraciones AS v",
        ]);

        foreach ((string alias, Type tipo) in s_alias)
        {
            IEntityType entidad = Entidad(tipo);

            encontradas.ShouldContain($"{entidad.GetSchema()}.{entidad.GetTableName()} AS {alias}", tipo.Name);
        }
    }

    /// <summary>Cada columna que nombra por su alias existe en la tabla de ese alias.</summary>
    [Fact]
    public void Cada_columna_que_nombra_existe_en_su_tabla()
    {
        Dictionary<string, string[]> esperadas = new(StringComparer.Ordinal)
        {
            ["c"] = ["empresa_id", "hasta_el_mes"],
            ["e"] = ["almacen_id", "articulo_id", "empresa_id", "id"],
            ["i"] = ["empresa_id", "existencia_id", "fisico", "mes", "valor"],
            ["l"] = ["articulo_id", "cantidad_introducida", "factor_a_unidad_base", "transferencia_id", "valor"],
            ["m"] = ["almacen_id", "articulo_id", "cantidad_en_unidad_base", "empresa_id", "fecha_de_operacion", "valor"],
            ["n"] = ["anula_a_id", "empresa_id", "fecha_de_recepcion"],
            ["t"] = [
                "almacen_destino_id", "anula_a_id", "empresa_id", "estado", "fecha_de_envio",
                "fecha_de_recepcion", "id",
            ],
            ["v"] = ["almacen_id", "articulo_id", "divisa", "empresa_id"],
        };

        foreach ((string alias, Type tipo) in s_alias)
        {
            List<string> encontradas = [.. Regex.Matches(
                    Sql,
                    $@"\b{alias}\.([a-z_]+)\b",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(1))
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

    /// <summary>No lleva punto y coma.</summary>
    [Fact]
    public void No_lleva_punto_y_coma()
    {
        // EF Core la envuelve en un `SELECT ... FROM (...)`, y ahí dentro un punto y coma es un
        // 42601 en ejecución (ADR-0043).
        Sql.ShouldNotContain(";");
    }

    /// <summary>
    /// Cada tabla compara la empresa contra el primer parámetro, salvo la línea de la transferencia,
    /// que no la lleva y es la de su transferencia.
    /// </summary>
    [Fact]
    public void Cada_tabla_compara_la_empresa_contra_el_primer_parametro()
    {
        foreach (string alias in new[] { "c", "e", "i", "m", "n", "t", "v" })
        {
            Posicion(alias, "empresa_id").ShouldBe("0", $"la empresa del alias {alias}");
        }

        // La línea no tiene empresa en el modelo: si la tuviera, tendría que compararla aquí.
        Entidad(typeof(LineaDeTransferencia)).GetProperties()
            .Select(propiedad => propiedad.Name)
            .ShouldNotContain("EmpresaId");
        Sql.ShouldContain("JOIN inventario.lineas_transferencia AS l ON l.transferencia_id = t.id");
    }

    /// <summary>
    /// La fecha se compara contra el segundo parámetro en el libro, en el mes anterior y en las tres
    /// fechas del tránsito, con el sentido que dice el ADR-0063.
    /// </summary>
    [Fact]
    public void La_fecha_se_compara_contra_el_segundo_parametro_con_su_sentido()
    {
        // El libro hasta el final del día D, incluido; desde el mes siguiente al de la instantánea.
        Sql.ShouldContain("AND m.fecha_de_operacion <= {1}");
        Sql.ShouldContain(
            "AND m.fecha_de_operacion >= coalesce((tramo.mes + interval '1 month')::date, '-infinity'::date)");

        // El mes M es el anterior al de D, o el corte si es anterior, y sin corte no hay mes.
        Sql.ShouldContain(
            "SELECT least((date_trunc('month', {1}::timestamp) - interval '1 month')::date, c.hasta_el_mes)");
        Sql.ShouldContain("AND i.mes = tramo.mes");

        // Enviada en D o antes, sin recibir en D, y sin un inverso que en D ya la hubiera devuelto.
        Sql.ShouldContain("AND t.fecha_de_envio <= {1}");
        Sql.ShouldContain("AND (t.fecha_de_recepcion IS NULL OR t.fecha_de_recepcion > {1})");
        Sql.ShouldContain("AND n.anula_a_id = t.id");
        Sql.ShouldContain("AND n.fecha_de_recepcion <= {1})");
        Sql.ShouldContain("AND t.anula_a_id IS NULL");
    }

    /// <summary>
    /// Lo que vuela se pasa a unidad base y se redondea como el libro, que es lo que dice la restricción
    /// de su tabla.
    /// </summary>
    [Fact]
    public void Lo_que_vuela_se_redondea_como_el_libro()
    {
        // La restricción del libro está en el modelo de diseño, que es el que se migra: el de ejecución
        // no guarda las restricciones.
        LosModelosDeCadaModulo.EntidadesDeDiseno(_api.Services)
            .Single(entidad => entidad.ClrType == typeof(MovimientoStock))
            .GetCheckConstraints()
            .Select(restriccion => restriccion.Sql)
            .ShouldContain(ConfiguracionDeMovimientoStock.CantidadPorFactor);

        string[] lados = ConfiguracionDeMovimientoStock.CantidadPorFactor.Split(" = ");

        // Primero que la igualdad tiene la forma que se lee: la base a la izquierda, la cuenta a la derecha.
        lados.Length.ShouldBe(2);
        lados[0].ShouldBe("cantidad_en_unidad_base");
        lados[1].ShouldContain("factor_a_unidad_base");

        // La línea guarda la cantidad introducida y su factor, y el libro, la cuenta de los dos: en el
        // tránsito tiene que salir la misma, o un envío en otra unidad no sumaría lo que salió.
        string cuenta = lados[1]
            .Replace("cantidad_introducida", "l.cantidad_introducida", StringComparison.Ordinal)
            .Replace("factor_a_unidad_base", "l.factor_a_unidad_base", StringComparison.Ordinal);

        Sql.ShouldContain($"SELECT sum({cuenta}) AS cantidad,");
    }

    /// <summary>
    /// La página se ordena dentro, para cortarla, y otra vez fuera, al componer, porque el orden de una
    /// subconsulta no llega garantizado.
    /// </summary>
    [Fact]
    public void La_pagina_se_ordena_dentro_para_cortarla_y_otra_vez_fuera()
    {
        // Dentro, por el par, antes de saltar y de cortar: sin él, la página sería la que diera el plan.
        Regex.IsMatch(
                Sql,
                @"ORDER BY p\.articulo_id, p\.almacen_id\s+OFFSET \{4\} LIMIT \{5\}",
                RegexOptions.None,
                TimeSpan.FromSeconds(1))
            .ShouldBeTrue("el orden de la página antes de cortarla");

        // Fuera, sobre lo que compone EF Core (ADR-0043). Ningún caso lo pone rojo: el plan de hoy
        // conserva el orden de la página a través de los cruces laterales, y lo que se pide aquí es
        // que no dependa de eso.
        string fuente = File.ReadAllText(Path.Combine(RaizDelRepositorio.Ruta(), Fuente));

        Regex.IsMatch(
                fuente,
                @"\.OrderBy\(fila => fila\.ArticuloId\)\s+\.ThenBy\(fila => fila\.AlmacenId\)\s+\.ToListAsync\(",
                RegexOptions.None,
                TimeSpan.FromSeconds(1))
            .ShouldBeTrue("el orden de la página al componer");
    }

    /// <summary>Lo que cuenta como borrador es lo que el modelo guarda como borrador.</summary>
    [Fact]
    public void Lo_que_cuenta_como_borrador_es_lo_que_el_modelo_guarda()
    {
        object? borrador = Entidad(typeof(Transferencia)).FindProperty(nameof(Transferencia.Estado))!
            .GetTypeMapping()
            .Converter!
            .ConvertToProvider(EstadoDeTransferencia.Borrador);

        // Primero que el modelo lo guarda como texto: un número no casaría con ningún literal.
        borrador.ShouldBe(nameof(EstadoDeTransferencia.Borrador));

        Sql.ShouldContain($"AND t.estado <> '{borrador}'");
    }

    /// <summary>El puerto no deja que quien llama elija la empresa.</summary>
    [Fact]
    public void El_puerto_no_deja_que_quien_llama_elija_la_empresa()
    {
        typeof(ILecturaDeSaldos).GetMethod(nameof(ILecturaDeSaldos.LeerAsync))!.GetParameters()
            .Select(parametro => parametro.Name!)
            .ShouldBe(["fecha", "almacenId", "articuloId", "paginacion", "cancelacion"]);
    }

    /// <summary>
    /// El valor que compara sale del inquilino y va en su sitio, y no hay otra sentencia cruda en el
    /// fichero.
    /// </summary>
    [Fact]
    public void El_valor_que_compara_sale_del_inquilino_y_va_en_su_sitio()
    {
        // Qué va en la posición `{0}` es una línea de C#, y se lee del fuente.
        string fuente = File.ReadAllText(Path.Combine(RaizDelRepositorio.Ruta(), Fuente));

        // Primero cuántas hay: una segunda sin comprobar pasaría por las de abajo sin que nada lo notara.
        Regex.Count(fuente, @"\.SqlQueryRaw<").ShouldBe(1);

        fuente.ShouldContain("Guid empresaId = inquilino.EmpresaDelFiltro ??");
        fuente.ShouldContain(
            "SqlDelSaldo, empresaId, fecha, almacenes, articulos, paginacion.Salto, paginacion.Tamanio)");
    }

    private static string Posicion(string alias, string columna)
    {
        Match condicion = Regex.Match(
            Sql,
            $@"\b(?:WHERE|AND) {alias}\.{Regex.Escape(columna)} = \{{(\d+)\}}",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        return condicion.Success ? condicion.Groups[1].Value : "(ninguna)";
    }

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
