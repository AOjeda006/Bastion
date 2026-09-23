using System.Text.RegularExpressions;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Ejercicios;

/// <summary>
/// Lo que las dos sentencias del cerrojo del ejercicio escriben a mano es lo que el modelo de EF
/// Core mapea, y cada una lleva la cláusula de bloqueo que le toca.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las cadenas existen porque el bloqueo no tiene traductor.</b> Ni <c>FOR SHARE</c> ni
/// <c>FOR UPDATE</c> se pueden pedir por el modelo, así que las dos sentencias van escritas a mano
/// —y la de Inventario, además, sobre una tabla que desde allí ni se ve: <c>Ejercicio</c> es de
/// <c>Organizacion.Domain</c>—. Una cadena no se entera de un renombrado. Es el mismo trato que
/// <c>LaSentenciaDeNumeracionNombraLaTablaDeVerdadTests</c> le da a la numeración, por el mismo
/// motivo y escrito igual para que se lea igual.
/// </para>
/// <para>
/// <b>Lo que se rompería es silencioso.</b> Una tabla renombrada en la migración deja la sentencia
/// apuntando a algo que no existe, y el fallo sale en ejecución —dentro de una confirmación o de un
/// cierre— sin que lo vea el compilador. Una columna renombrada es peor cuando el nombre sigue
/// existiendo con otro significado: <c>fecha_de_fin</c> comparada contra la columna equivocada no
/// falla, contesta otra cosa.
/// </para>
/// <para>
/// <b>Y la cláusula intercambiada sería peor que cualquiera de las dos.</b> Con <c>FOR SHARE</c> en
/// el cierre, cerrar dejaría de esperar a las confirmaciones en vuelo —dos compartidos conviven— y
/// el mecanismo entero quedaría de adorno, sin que nada fallase. Con <c>FOR UPDATE</c> al
/// confirmar, dos documentos del mismo ejercicio dejarían de poder confirmarse a la vez, que es la
/// operación normal. Por eso el reparto se afirma por su nombre y en los dos sentidos.
/// </para>
/// <para>
/// <b>Se compara contra el modelo ya construido, que es quien manda</b>, y no contra la migración:
/// si el modelo y la base divergieran, eso lo dice <c>comprobar-migraciones.sh</c>, que es su
/// trabajo y no el de aquí.
/// </para>
/// </remarks>
public sealed class LaSentenciaDelEjercicioNombraLaTablaDeVerdadTests : IDisposable
{
    // Las columnas que las dos sentencias nombran con alias, por su alias. Lista CERRADA y
    // comparada en los dos sentidos: añadir una condición sobre otra columna obliga a pasar por
    // aquí, que es donde alguien decide si esa columna es de verdad parte de la regla. Y una que
    // sobre delata la condición que se quitó sin quitar su línea.
    private static readonly string[] s_columnasQueLasSentenciasNombran =
    [
        "e.empresa_id",
        "e.estado",
        "e.fecha_de_fin",
        "e.fecha_de_inicio",
        "e.id",
    ];

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Las_cadenas_del_puerto_de_Inventario_son_las_del_modelo()
    {
        IEntityType ejercicio = Entidad();

        LosEjerciciosDesdeInventario.Esquema.ShouldBe(ejercicio.GetSchema());
        LosEjerciciosDesdeInventario.Tabla.ShouldBe(ejercicio.GetTableName());
        LosEjerciciosDesdeInventario.ColumnaDeInicio.ShouldBe(
            Columna(ejercicio, nameof(Ejercicio.FechaDeInicio)));
        LosEjerciciosDesdeInventario.ColumnaDeFin.ShouldBe(
            Columna(ejercicio, nameof(Ejercicio.FechaDeFin)));
        LosEjerciciosDesdeInventario.ColumnaDeEstado.ShouldBe(
            Columna(ejercicio, nameof(Ejercicio.Estado)));
    }

    [Fact]
    public void Las_cadenas_del_cerrojo_de_Organizacion_son_las_del_modelo()
    {
        IEntityType ejercicio = Entidad();

        CerrojoDeEjercicios.Esquema.ShouldBe(ejercicio.GetSchema());
        CerrojoDeEjercicios.Tabla.ShouldBe(ejercicio.GetTableName());
        CerrojoDeEjercicios.ColumnaDeEstado.ShouldBe(
            Columna(ejercicio, nameof(Ejercicio.Estado)));
        CerrojoDeEjercicios.ColumnaDeEmpresa.ShouldBe(
            Columna(ejercicio, nameof(Ejercicio.EmpresaId)));
    }

    [Fact]
    public void Cada_columna_que_las_sentencias_nombran_existe_en_la_tabla()
    {
        // Se EXTRAEN de las cadenas, no se escriben otra vez: una lista copiada a mano seguiría
        // verde el día que alguien añadiera `AND e.lo_que_sea = ...` a una sentencia de verdad.
        List<string> encontradas = [.. Regex
            .Matches(
                LosEjerciciosDesdeInventario.SqlDelEstadoConCerrojo + " " +
                CerrojoDeEjercicios.SqlDelEstadoConCerrojo,
                @"\be\.([a-z_]+)\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(1))
            .Select(coincidencia => coincidencia.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        // Primero el barrido se afirma a sí mismo (ADR-0020): si la expresión dejara de casar, lo
        // de abajo recorrería una lista vacía y saldría verde sin haber mirado ni una columna.
        encontradas.ShouldBe(s_columnasQueLasSentenciasNombran);

        IEntityType ejercicio = Entidad();
        IReadOnlyList<string> mapeadas = ColumnasDe(ejercicio);

        List<string> inexistentes =
            [.. encontradas.Where(nombrada => !mapeadas.Contains(nombrada[2..], StringComparer.Ordinal))];

        inexistentes.ShouldBeEmpty(
            "las sentencias del ejercicio nombran columnas que el modelo no mapea: " +
            string.Join(", ", inexistentes));
    }

    [Fact]
    public void El_compartido_es_el_de_confirmar_y_el_exclusivo_el_de_cerrar()
    {
        LosEjerciciosDesdeInventario.SqlDelEstadoConCerrojo.ShouldEndWith(" FOR SHARE");
        LosEjerciciosDesdeInventario.SqlDelEstadoConCerrojo.ShouldNotContain("FOR UPDATE");

        CerrojoDeEjercicios.SqlDelEstadoConCerrojo.ShouldEndWith(" FOR UPDATE");
        CerrojoDeEjercicios.SqlDelEstadoConCerrojo.ShouldNotContain("FOR SHARE");
    }

    [Fact]
    public void Ninguna_de_las_dos_lleva_punto_y_coma_final()
    {
        // EF Core compone estas cadenas DENTRO de otra sentencia —las envuelve en un
        // `SELECT ... FROM (...)`—, así que un punto y coma ahí dentro es un 42601 en ejecución:
        // no lo ve el compilador, y no lo ve ningún carril que no baje a PostgreSQL.
        LosEjerciciosDesdeInventario.SqlDelEstadoConCerrojo.ShouldNotContain(";");
        CerrojoDeEjercicios.SqlDelEstadoConCerrojo.ShouldNotContain(";");
    }

    [Fact]
    public void El_estado_que_las_dos_traducen_se_guarda_como_texto()
    {
        // LAS DOS MITADES SON LA MISMA DECISIÓN. Los dos lados traducen la columna por el NOMBRE
        // del valor del enumerado, y eso solo casa si el estado se guarda como TEXTO. Cambiar la
        // conversión a entero no rompería nada visible aquí: las dos lecturas empezarían a recibir
        // «0» y «1», y el `switch` de Inventario reventaría en la primera confirmación —en
        // ejecución, dentro de un caso de uso que no tenía nada que ver—.
        IProperty estado = Entidad().FindProperty(nameof(Ejercicio.Estado))!;

        (estado.GetTypeMapping().Converter?.ProviderClrType ?? estado.ClrType)
            .ShouldBe(typeof(string));
    }

    private IEntityType Entidad()
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        return alcance.ServiceProvider.GetRequiredService<OrganizacionDbContext>()
            .Model.FindEntityType(typeof(Ejercicio))
            ?? throw new InvalidOperationException("Ejercicio no está en el modelo.");
    }

    private static string Columna(IEntityType entidad, string propiedad) =>
        entidad.FindProperty(propiedad)!.GetColumnName(
            StoreObjectIdentifier.Create(entidad, StoreObjectType.Table)!.Value)!;

    private static IReadOnlyList<string> ColumnasDe(IEntityType entidad) =>
        [.. entidad.GetProperties().Select(propiedad => Columna(entidad, propiedad.Name))];
}
