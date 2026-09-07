using Bastion.Terceros.Infrastructure.Persistencia;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Persistencia;

/// <summary>
/// El esquema de Terceros, mirado <b>en la base</b>. Y con un motivo que los otros módulos no
/// tienen: aquí hay un índice que el modelo de EF Core no conoce.
/// </summary>
/// <remarks>
/// <para>
/// <b>El índice único de (empresa, país, número) está escrito a mano en la migración</b>, porque
/// EF Core 10 no sabe indexar miembros de un tipo complejo: ni por selector
/// —<c>fila =&gt; new { fila.EmpresaId, fila.Identificacion.Pais }</c>, que no es una expresión de
/// acceso a miembro válida— ni por nombres —<c>HasIndex("EmpresaId", "Identificacion.Pais")</c>,
/// que no encuentra la propiedad—. Se probaron las dos y las dos fallan.
/// </para>
/// <para>
/// <b>Eso deja un agujero de vigilancia, y este fichero es quien lo tapa.</b> Como el modelo no
/// declara el índice, <c>has-pending-model-changes</c> no puede echarlo en falta: alguien que
/// borrara esas líneas de la migración dejaría el modelo y la base de acuerdo, la comprobación de
/// migraciones en verde, y la unicidad del maestro de terceros sin nadie que la sostuviera. Aquí
/// se lee de <c>pg_index</c>, que es lo que hay de verdad después de migrar.
/// </para>
/// <para>
/// <b>Y se afirma además que su predicado parcial está vacío</b>, que no es un detalle: es la
/// decisión del ítem. Un <c>WHERE bloqueado = false</c> convertiría la unicidad en parcial, el
/// identificador de un tercero bloqueado quedaría libre, y desbloquearlo podría chocar contra la
/// ficha que ocupó su sitio — un empate con datos personales dentro que alguien tendría que
/// deshacer a mano. Lo comprueba por el efecto <c>ElConflictoQueNoRevelaTests</c>; aquí se
/// comprueba la causa.
/// </para>
/// <para>
/// <b>Desde el ítem 1.6 el esquema tiene cuatro tablas, y esa es la mitad que este fichero añade.</b>
/// Lo que cuelga del tercero trae tres restricciones que <b>solo existen en la base</b> —el índice
/// parcial de la cuenta preferente y los tres <c>CHECK</c>—, y ninguna de las tres la puede ver un
/// test de dominio: los invariantes del agregado se comprueban en
/// <c>LoQueCuelgaDelTerceroTests</c>, pero un agregado en memoria no dice nada de lo que la base
/// aceptaría por otro camino —una importación, una migración, una corrección a mano—. Aquí se lee
/// de <c>pg_index</c> y de <c>pg_constraint</c>, que es lo que hay después de migrar.
/// </para>
/// <para>
/// <b>Lo que estas comprobaciones NO son</b>: no meten una fila para ver si la restricción muerde.
/// Leen su definición. Es más débil que verificar por el efecto y se dice: cazan que la restricción
/// se haya borrado o se haya escrito mal, que es el fallo que de verdad ocurre —una migración
/// reescrita, un <c>CHECK</c> que se cae al regenerar—, y no cazarían un motor que la ignorase.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class EsquemaDeTercerosTests(PostgresConTodosLosModulos postgres)
{
    private const string Tabla = "terceros";
    private const string Contactos = "contactos";
    private const string Cuentas = "cuentas_bancarias";
    private const string Condiciones = "condiciones_pago";

    [Fact]
    public async Task La_tabla_del_modulo_esta_en_SU_esquema_y_en_snake_case()
    {
        IReadOnlyList<(string Primera, string Segunda)> encontradas = await ConsultarAsync(
            $"""
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema = '{TercerosDbContext.Esquema}'
            ORDER BY table_name
            """);

        // Las cuatro tablas del módulo y su historial, y nada más. La lista se compara ENTERA: una
        // tabla de sorpresa aquí sería una entidad que se ha colado sin pasar por el plan, y una
        // que falte, una migración que no se ha aplicado. Eran dos hasta el 1.6; las otras tres son
        // lo que cuelga del tercero, cada una con su agregado padre.
        encontradas.Select(fila => fila.Segunda).ShouldBe(
            [TercerosDbContext.TablaDelHistorial, Tabla, Contactos, Cuentas, Condiciones],
            ignoreOrder: true);
    }

    /// <summary>
    /// El índice único de (empresa, país, número), leído de la base y con su predicado vacío.
    /// </summary>
    [Fact]
    public async Task La_unicidad_del_identificador_esta_EN_LA_BASE_y_abarca_tambien_lo_bloqueado()
    {
        IReadOnlyList<(string Nombre, string Definicion)> indices = await ConsultarAsync(
            $"""
            SELECT indice.relname, pg_get_indexdef(indice.oid)
            FROM pg_index AS declaracion
            JOIN pg_class AS indice ON indice.oid = declaracion.indexrelid
            JOIN pg_class AS tabla ON tabla.oid = declaracion.indrelid
            JOIN pg_namespace AS esquema ON esquema.oid = tabla.relnamespace
            WHERE esquema.nspname = '{TercerosDbContext.Esquema}'
              AND tabla.relname = '{Tabla}'
              AND declaracion.indisunique
              AND NOT declaracion.indisprimary
              AND declaracion.indpred IS NULL
            """);

        // UNO, y con las tres columnas en ese orden: la empresa delante, porque toda consulta del
        // módulo entra por ella (R8) y un índice que la llevara detrás no serviría para nada más.
        indices.Count.ShouldBe(
            1,
            "falta el índice único de (empresa, país, número) SIN predicado parcial. Está escrito " +
            "a mano en la migración porque EF Core no sabe indexar miembros de un tipo complejo, " +
            "así que `has-pending-model-changes` no lo echa de menos y este test es lo único que " +
            "lo nota. Lo que hay en la tabla: " + await IndicesDeLaTablaAsync(Tabla));

        indices[0].Definicion.ShouldContain("(empresa_id, identificacion_pais, identificacion_numero)");
        indices[0].Definicion.ShouldNotContain(" WHERE ");
    }

    /// <summary>
    /// Una sola cuenta preferente por tercero, y su índice <b>sí</b> es parcial.
    /// </summary>
    /// <remarks>
    /// Es el contraste con el de arriba, y por eso están en el mismo fichero: el del identificador
    /// no puede llevar predicado y este no puede no llevarlo. Sin el filtro
    /// <c>WHERE es_preferente</c>, la unicidad se aplicaría a <b>todas</b> las cuentas del tercero
    /// y un tercero no podría tener dos. Con él, se aplica solo a las marcadas, que es la
    /// restricción que se quiere: como mucho una preferente, tantas cuentas como haga falta.
    /// <para>
    /// <b>Y ve las filas bloqueadas, igual que el del identificador: es la misma decisión del 1.5.</b>
    /// Aquí, además, no podría ser de otra manera — el bloqueo del art. 32 vive en el padre, no en
    /// la cuenta, así que dos cuentas que compiten por ser la preferente están siempre en el mismo
    /// estado.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_cuenta_preferente_es_UNICA_por_tercero_y_su_indice_SI_es_parcial()
    {
        IReadOnlyList<(string Nombre, string Definicion)> indices = await ConsultarAsync(
            $"""
            SELECT indice.relname, pg_get_indexdef(indice.oid)
            FROM pg_index AS declaracion
            JOIN pg_class AS indice ON indice.oid = declaracion.indexrelid
            JOIN pg_class AS tabla ON tabla.oid = declaracion.indrelid
            JOIN pg_namespace AS esquema ON esquema.oid = tabla.relnamespace
            WHERE esquema.nspname = '{TercerosDbContext.Esquema}'
              AND tabla.relname = '{Cuentas}'
              AND declaracion.indisunique
              AND NOT declaracion.indisprimary
              AND declaracion.indpred IS NOT NULL
            """);

        indices.Count.ShouldBe(
            1,
            "falta el índice único PARCIAL de la cuenta preferente. Lo que hay en la tabla: " +
            await IndicesDeLaTablaAsync(Cuentas));

        indices[0].Nombre.ShouldBe("ix_cuentas_bancarias_una_preferente_por_tercero");
        indices[0].Definicion.ShouldContain("(tercero_id)");
        indices[0].Definicion.ShouldContain("WHERE es_preferente");
    }

    /// <summary>
    /// El mismo IBAN no se cuelga dos veces de la misma ficha, y eso también está en la base.
    /// </summary>
    [Fact]
    public async Task El_iban_no_se_repite_dentro_de_la_misma_ficha()
    {
        IReadOnlyList<(string Nombre, string Definicion)> indices = await ConsultarAsync(
            $"""
            SELECT indice.relname, pg_get_indexdef(indice.oid)
            FROM pg_index AS declaracion
            JOIN pg_class AS indice ON indice.oid = declaracion.indexrelid
            JOIN pg_class AS tabla ON tabla.oid = declaracion.indrelid
            JOIN pg_namespace AS esquema ON esquema.oid = tabla.relnamespace
            WHERE esquema.nspname = '{TercerosDbContext.Esquema}'
              AND tabla.relname = '{Cuentas}'
              AND declaracion.indisunique
              AND NOT declaracion.indisprimary
              AND declaracion.indpred IS NULL
            """);

        // La unicidad es por (tercero, IBAN) y NO global: la misma cuenta puede ser de dos terceros
        // distintos —un autónomo que factura y cobra por la suya, una matriz y su filial— y una
        // unicidad global lo prohibiría inventándose una regla que nadie ha pedido.
        indices.Count.ShouldBe(1, "lo que hay en la tabla: " + await IndicesDeLaTablaAsync(Cuentas));
        indices[0].Definicion.ShouldContain("(tercero_id, iban)");
    }

    /// <summary>
    /// Los tres <c>CHECK</c> del ítem, leídos de <c>pg_constraint</c>.
    /// </summary>
    /// <remarks>
    /// El del plazo es el que importa de verdad: <b>el tope de sesenta días es imperativo y no se
    /// amplía por acuerdo</b> (art. 4 de la Ley 3/2004), así que no puede vivir solo en el dominio.
    /// Una importación de CSV, una migración de datos o una corrección a mano entran por debajo del
    /// agregado, y si el único guardián fuera <c>CondicionPago</c>, un 90 escrito por ahí se
    /// quedaría guardado para siempre pareciendo legal.
    /// </remarks>
    [Theory]
    [InlineData(Condiciones, "ck_condiciones_pago_plazo_legal", "dias_de_plazo")]
    [InlineData(Tabla, "ck_terceros_territorio_fiscal", "territorio_fiscal")]
    [InlineData(Tabla, "ck_terceros_limite_credito_completo", "limite_credito_cantidad")]
    public async Task Las_reglas_que_no_se_pueden_esquivar_estan_EN_LA_BASE(
        string tabla,
        string restriccion,
        string columnaQueMenciona)
    {
        IReadOnlyList<(string Nombre, string Definicion)> encontradas = await ConsultarAsync(
            $"""
            SELECT restriccion.conname, pg_get_constraintdef(restriccion.oid)
            FROM pg_constraint AS restriccion
            JOIN pg_class AS relacion ON relacion.oid = restriccion.conrelid
            JOIN pg_namespace AS esquema ON esquema.oid = relacion.relnamespace
            WHERE esquema.nspname = '{TercerosDbContext.Esquema}'
              AND relacion.relname = '{tabla}'
              AND restriccion.contype = 'c'
              AND restriccion.conname = '{restriccion}'
            """);

        encontradas.Count.ShouldBe(1, $"falta el CHECK {restriccion} en {tabla}");
        encontradas[0].Definicion.ShouldContain(columnaQueMenciona);
    }

    /// <summary>El tope de los sesenta días está escrito en el CHECK, y es sesenta.</summary>
    /// <remarks>
    /// Aparte del test de arriba, y no es repetirlo: aquel dice que la restricción existe y nombra
    /// su columna; este dice <b>cuál es el número</b>. Un CHECK que pusiera 90 pasaría el primero.
    /// </remarks>
    [Fact]
    public async Task El_tope_del_plazo_de_pago_es_sesenta_dias_naturales()
    {
        IReadOnlyList<(string Nombre, string Definicion)> encontradas = await ConsultarAsync(
            $"""
            SELECT restriccion.conname, pg_get_constraintdef(restriccion.oid)
            FROM pg_constraint AS restriccion
            JOIN pg_class AS relacion ON relacion.oid = restriccion.conrelid
            JOIN pg_namespace AS esquema ON esquema.oid = relacion.relnamespace
            WHERE esquema.nspname = '{TercerosDbContext.Esquema}'
              AND relacion.relname = '{Condiciones}'
              AND restriccion.conname = 'ck_condiciones_pago_plazo_legal'
            """);

        encontradas.Count.ShouldBe(1);
        encontradas[0].Definicion.ShouldContain("60");
        encontradas[0].Definicion.ShouldNotContain("90");
    }

    [Theory]
    [InlineData("identificacion_pais", "character varying")]
    [InlineData("identificacion_numero", "character varying")]
    [InlineData("identificacion_verificacion", "text")]
    public async Task El_identificador_fiscal_son_TRES_columnas_y_no_una_cadena_suelta(
        string columna,
        string tipo)
    {
        // Aplanado en la misma tabla y no en una aparte: es un tipo complejo (ADR-0016), o sea un
        // valor sin identidad propia. Con el estado de verificación al lado del número, no hay
        // forma de leer uno y olvidarse del otro — que es justo lo que este ítem existe para
        // impedir.
        (await TipoDeColumnaAsync(Tabla, columna)).ShouldBe(tipo);
    }

    /// <summary>El régimen fiscal son cuatro columnas de la ficha, no una tabla aparte.</summary>
    /// <remarks>
    /// Es un valor sin identidad propia —el régimen de un tercero no existe sin ese tercero—, así
    /// que va aplanado como la identificación. Y el territorio viaja como <b>texto</b>, no como el
    /// ordinal del enumerado: un entero en la base hace que reordenar los valores del <c>enum</c>
    /// reinterprete en silencio todas las filas guardadas.
    /// </remarks>
    [Theory]
    [InlineData("territorio_fiscal", "character varying")]
    [InlineData("recargo_de_equivalencia", "boolean")]
    [InlineData("criterio_de_caja", "boolean")]
    [InlineData("sujeto_a_retencion_irpf", "boolean")]
    public async Task El_regimen_fiscal_son_CUATRO_columnas_de_la_ficha(string columna, string tipo) =>
        (await TipoDeColumnaAsync(Tabla, columna)).ShouldBe(tipo);

    /// <summary>El límite de crédito es una cantidad exacta con su divisa al lado.</summary>
    /// <remarks>
    /// <c>numeric</c> y no <c>double precision</c>, y desde luego no el <c>money</c> del motor: R6
    /// no admite coma flotante en nada que sea dinero. Y la divisa <b>viaja con el importe</b>, no
    /// se hereda de la empresa: si se heredara, cambiar la divisa base reinterpretaría todos los
    /// límites guardados sin tocar una fila.
    /// </remarks>
    [Theory]
    [InlineData("limite_credito_cantidad", "numeric")]
    [InlineData("limite_credito_divisa", "character varying")]
    public async Task El_limite_de_credito_es_numeric_y_lleva_su_divisa(string columna, string tipo) =>
        (await TipoDeColumnaAsync(Tabla, columna)).ShouldBe(tipo);

    /// <summary>La escala del importe es la de R6, leída de la base.</summary>
    [Fact]
    public async Task La_escala_del_limite_de_credito_es_la_de_R6()
    {
        IReadOnlyList<(string Precision, string Escala)> filas = await ConsultarAsync(
            $"""
            SELECT numeric_precision::text, numeric_scale::text
            FROM information_schema.columns
            WHERE table_schema = '{TercerosDbContext.Esquema}'
              AND table_name = '{Tabla}' AND column_name = 'limite_credito_cantidad'
            """);

        filas.Count.ShouldBe(1);
        filas[0].Precision.ShouldBe("18");
        filas[0].Escala.ShouldBe("4");
    }

    [Theory]
    [InlineData("bloqueado", "boolean")]
    [InlineData("bloqueado_en", "timestamp with time zone")]
    [InlineData("motivo_del_bloqueo", "text")]
    [InlineData("creado_en", "timestamp with time zone")]
    [InlineData("modificado_en", "timestamp with time zone")]
    public async Task El_bloqueo_y_las_marcas_son_las_MISMAS_columnas_que_en_los_demas_modulos(
        string columna,
        string tipo)
    {
        // Los mismos tres campos del bloqueo y las mismas dos marcas de R14, con los mismos
        // nombres. No es simetría decorativa: `LaFilaBloqueadaSigueEnLaBase` y el listado del
        // art. 32 leen por nombre de columna, y un módulo que las llamara de otra manera se
        // quedaría fuera de esas comprobaciones sin que nada se pusiera rojo.
        (await TipoDeColumnaAsync(Tabla, columna)).ShouldBe(tipo);
    }

    /// <summary>
    /// Lo que cuelga del tercero se borra con él, y eso lo dice la base con <c>ON DELETE CASCADE</c>.
    /// </summary>
    /// <remarks>
    /// No es una comodidad: es el <b>derecho de supresión</b> del art. 17 del RGPD. Un contacto es
    /// una persona física, y si la ficha se suprime de verdad y sus contactos se quedan, quedan
    /// huérfanos —con su nombre y su teléfono— en una tabla a la que ya no llega ninguna consulta
    /// del módulo. Nadie los volvería a mirar y nadie los borraría.
    /// </remarks>
    [Theory]
    [InlineData(Contactos, "fk_contactos_terceros_tercero_id")]
    [InlineData(Cuentas, "fk_cuentas_bancarias_terceros_tercero_id")]
    [InlineData(Condiciones, "fk_condiciones_pago_terceros_tercero_id")]
    public async Task Lo_que_cuelga_se_borra_CON_la_ficha_y_no_se_queda_huerfano(
        string tabla,
        string clave)
    {
        IReadOnlyList<(string Nombre, string Definicion)> encontradas = await ConsultarAsync(
            $"""
            SELECT restriccion.conname, pg_get_constraintdef(restriccion.oid)
            FROM pg_constraint AS restriccion
            JOIN pg_class AS relacion ON relacion.oid = restriccion.conrelid
            JOIN pg_namespace AS esquema ON esquema.oid = relacion.relnamespace
            WHERE esquema.nspname = '{TercerosDbContext.Esquema}'
              AND relacion.relname = '{tabla}'
              AND restriccion.contype = 'f'
              AND restriccion.conname = '{clave}'
            """);

        encontradas.Count.ShouldBe(1, $"falta la clave ajena {clave} en {tabla}");
        encontradas[0].Definicion.ShouldContain("ON DELETE CASCADE");
    }

    /// <summary>
    /// Lo que toda fila lleva es <c>NOT NULL</c> y sin <c>DEFAULT</c>, también en las cuatro
    /// columnas que el 1.6 añadió a una tabla con filas dentro.
    /// </summary>
    /// <remarks>
    /// Es la regla que ya tiene Organización, traída aquí por un motivo concreto: las cuatro
    /// columnas del régimen fiscal se añadieron a <c>terceros</c> cuando ya podía haber filas, y el
    /// andamiaje que EF Core genera para ese caso es <c>ADD COLUMN ... NOT NULL DEFAULT x</c>, que
    /// <b>deja el DEFAULT puesto para siempre</b>. Sería una forma nueva de valor generado por el
    /// servidor —cuando lo único que lo genera son los testigos del ADR-0015— y una diferencia
    /// entre esquema y modelo que ningún barrido sobre el modelo puede ver. La migración añade,
    /// rellena y cierra en tres pasos precisamente para que esto salga vacío; este test es quien
    /// dice si lo hizo.
    /// </remarks>
    [Theory]
    [InlineData(Tabla, "territorio_fiscal")]
    [InlineData(Tabla, "recargo_de_equivalencia")]
    [InlineData(Tabla, "criterio_de_caja")]
    [InlineData(Tabla, "sujeto_a_retencion_irpf")]
    [InlineData(Tabla, "creado_en")]
    [InlineData(Tabla, "modificado_en")]
    [InlineData(Contactos, "creado_en")]
    [InlineData(Contactos, "modificado_en")]
    [InlineData(Cuentas, "creado_en")]
    [InlineData(Cuentas, "modificado_en")]
    [InlineData(Cuentas, "es_preferente")]
    [InlineData(Condiciones, "creado_en")]
    [InlineData(Condiciones, "modificado_en")]
    [InlineData(Condiciones, "dias_de_plazo")]
    public async Task Lo_que_toda_fila_tiene_que_llevar_es_NOT_NULL_y_sin_DEFAULT(
        string tabla,
        string columna)
    {
        (await NuloYPorOmisionAsync(tabla, columna)).ShouldBe(("NO", null));
    }

    /// <summary>El límite de crédito, en cambio, sí admite nulo — y es una decisión.</summary>
    /// <remarks>
    /// No tener límite y tener un límite de cero son cosas distintas: cero es «no se le fía ni un
    /// euro» y nulo es «no se le controla el crédito». Por eso las dos columnas son opcionales, y
    /// por eso hay un <c>CHECK</c> que obliga a que estén o falten <b>las dos juntas</b>: una
    /// cantidad sin divisa sería el único importe del sistema que no dice de qué es.
    /// </remarks>
    [Theory]
    [InlineData("limite_credito_cantidad")]
    [InlineData("limite_credito_divisa")]
    public async Task El_limite_de_credito_admite_nulo_porque_no_tenerlo_no_es_tenerlo_a_cero(
        string columna)
    {
        (await NuloYPorOmisionAsync(Tabla, columna)).ShouldBe(("YES", null));
    }

    [Fact]
    public async Task La_empresa_se_guarda_como_identificador_y_NO_como_clave_ajena()
    {
        (await TipoDeColumnaAsync(Tabla, "empresa_id")).ShouldBe("uuid");

        IReadOnlyList<(string Primera, string Segunda)> cruzadas = await ConsultarAsync(
            $"""
            SELECT origen.table_schema, origen.table_name
            FROM information_schema.referential_constraints AS referencia
            JOIN information_schema.table_constraints AS origen
              ON origen.constraint_name = referencia.constraint_name
            JOIN information_schema.table_constraints AS destino
              ON destino.constraint_name = referencia.unique_constraint_name
            WHERE origen.table_schema = '{TercerosDbContext.Esquema}'
              AND destino.table_schema <> '{TercerosDbContext.Esquema}'
            """);

        // Quien dice si esa empresa existe y está operativa es Organización por sus Contracts, y
        // lo comprueba `CrearTercero` antes de construir el agregado (ADR-0024). Una clave ajena a
        // `organizacion.empresas` ataría los dos módulos a migrarse y desplegarse juntos para
        // siempre, que es exactamente la frontera que el §4 levanta.
        //
        // La pregunta es «ninguna que SALGA del esquema», no «ninguna»: desde el 1.6 hay tres
        // dentro —las de lo que cuelga— y son justo lo contrario de un problema. Atan un agregado
        // con sus hijos, que es donde una clave ajena tiene sentido.
        cruzadas.ShouldBeEmpty("no puede haber claves ajenas entre esquemas (§4, regla 4)");
    }

    private async Task<string> IndicesDeLaTablaAsync(string tabla)
    {
        IReadOnlyList<(string Nombre, string Definicion)> todos = await ConsultarAsync(
            $"""
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE schemaname = '{TercerosDbContext.Esquema}' AND tablename = '{tabla}'
            """);

        return Environment.NewLine + string.Join(
            Environment.NewLine, todos.Select(indice => "   " + indice.Definicion));
    }

    private async Task<string?> TipoDeColumnaAsync(string tabla, string columna)
    {
        IReadOnlyList<(string Primera, string Segunda)> filas = await ConsultarAsync(
            $"""
            SELECT data_type, data_type
            FROM information_schema.columns
            WHERE table_schema = '{TercerosDbContext.Esquema}'
              AND table_name = '{tabla}' AND column_name = '{columna}'
            """);

        return filas.Count == 0 ? null : filas[0].Primera;
    }

    private async Task<(string Nulo, string? PorOmision)> NuloYPorOmisionAsync(
        string tabla,
        string columna)
    {
        IReadOnlyList<(string Primera, string Segunda)> filas = await ConsultarAsync(
            $"""
            SELECT is_nullable, COALESCE(column_default, '')
            FROM information_schema.columns
            WHERE table_schema = '{TercerosDbContext.Esquema}'
              AND table_name = '{tabla}' AND column_name = '{columna}'
            """);

        filas.Count.ShouldBe(1, $"no existe la columna {tabla}.{columna}");

        return (filas[0].Primera, filas[0].Segunda.Length == 0 ? null : filas[0].Segunda);
    }

    private async Task<IReadOnlyList<(string Primera, string Segunda)>> ConsultarAsync(string consulta)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(consulta, conexion);
        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        List<(string, string)> filas = [];

        while (await lector.ReadAsync())
        {
            filas.Add((lector.GetString(0), lector.GetString(1)));
        }

        return filas;
    }
}
