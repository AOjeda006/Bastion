using Bastion.Api.FunctionalTests.Persistencia;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Errores;

/// <summary>
/// Que la lista de restricciones traducidas a una regla de negocio y las restricciones que de
/// verdad existen digan lo mismo, y que la lista sea exactamente la que alguien decidió.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el gemelo del barrido de los índices</b> (<c>CadaIndiceTraducidoSeJustificaTests</c>), y por
/// el mismo motivo: el borde reconoce la restricción por el <c>ConstraintName</c> que trae la
/// excepción, que es una cadena escrita a mano. Si una migración la renombra y nadie toca la
/// declaración, el stock negativo vuelve a salir como <c>500</c> el primer día que dos salidas se
/// crucen, y no falla nada antes.
/// </para>
/// <para>
/// <b>Desde el 2.9 la lista admite índices únicos</b> (ADR-0048 §3), así que cada declaración se
/// busca en el modelo con su clase: un <c>CHECK</c> entre los <c>CHECK</c>, y un índice entre los
/// índices únicos. Y un nombre no puede estar a la vez en esta lista y en la de la carrera perdida,
/// que traduce el mismo <c>23505</c> a un <c>412</c>.
/// </para>
/// <para>
/// <b>Sin base de datos</b>: el modelo se construye antes de abrir ninguna conexión, así que esto
/// sale en el carril rápido.
/// </para>
/// </remarks>
public sealed class CadaRestriccionTraducidaSeJustificaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    /// <summary>Apaga la API levantada para este barrido.</summary>
    public void Dispose() => _api.Dispose();

    /// <summary>
    /// Toda restricción declarada existe en el modelo con la clase que se declaró, y dice por qué
    /// solo la guarda el motor.
    /// </summary>
    [Fact]
    public void Toda_restriccion_declarada_existe_en_el_modelo()
    {
        IReadOnlyDictionary<string, RestriccionDeclarada> declaradas = Declaradas();

        declaradas.ShouldNotBeEmpty(
            "sin una sola declaración, este barrido y el de abajo salen verdes por no haber " +
            "mirado nada (ADR-0020). El día que no quede ninguna, se borra el mecanismo entero");

        HashSet<string> comprobaciones = RestriccionesDelModelo();
        HashSet<string> unicos = IndicesUnicosDelModelo();

        comprobaciones.ShouldNotBeEmpty(
            "el modelo no tiene ninguna restricción, así que el barrido no está leyendo el modelo");
        unicos.ShouldNotBeEmpty(
            "el modelo no tiene ningún índice único, así que el barrido no está leyendo el modelo");

        List<string> mal = [];

        foreach ((string restriccion, RestriccionDeclarada declarada) in declaradas)
        {
            HashSet<string> dondeBuscar = declarada.Clase switch
            {
                ClaseDeRestriccion.Comprobacion => comprobaciones,
                ClaseDeRestriccion.Unicidad => unicos,
                _ => [],
            };

            if (!dondeBuscar.Contains(restriccion))
            {
                mal.Add($"«{restriccion}» está declarada como {declarada.Clase} y no hay ninguna " +
                        "de esa clase con ese nombre en ningún modelo");
            }

            declarada.Motivo.Trim().Length.ShouldBeGreaterThan(
                40, $"«{restriccion}» se declara sin decir por qué no se puede comprobar antes");
        }

        mal.ShouldBeEmpty(
            "la lista de restricciones traducidas nombra restricciones que el modelo no tiene");
    }

    /// <summary>
    /// Y ninguna restricción se traduce por accidente, ni con otra clase ni con otro error: la
    /// lista, con su clase y su código, es la que está escrita aquí.
    /// </summary>
    /// <remarks>
    /// El código va en la lista porque es la mitad de la decisión. Traducir una restricción a una
    /// regla de negocio le dice al cliente «esto no se puede hacer así», y el código le dice qué.
    /// Cambiarlo es cambiar un contrato publicado, y tiene que costar tocar dos ficheros.
    /// </remarks>
    [Fact]
    public void Ninguna_restriccion_se_traduce_sin_estar_en_esta_lista()
    {
        // Las restricciones cuya violación NO es un defecto sino una regla que solo el motor puede
        // guardar, con su clase y el código con el que salen. Una por línea, con su motivo en el
        // módulo.
        string[] permitidas =
        [
            // Ítem 2.8: el stock no baja de cero. Una comprobación previa la cruzan dos salidas
            // simultáneas juntas; el CHECK se evalúa con la fila ya bloqueada (ADR-0046 §4).
            "ck_existencias_fisico_no_negativo (Comprobacion) → stock-insuficiente",

            // Ítem 2.9: una serie no está dos veces en su ubicación, ni en dos sitios. Las dos, al
            // mismo error, porque para quien confirma son lo mismo (ADR-0048 §3).
            "ck_existencias_numero_de_serie_como_mucho_una (Comprobacion) → numero-de-serie-en-existencias",
            "ix_existencias_numero_de_serie_en_un_sitio (Unicidad) → numero-de-serie-en-existencias",

            // Ítem 2.10: un GTIN es de un solo artículo en cada empresa. La comprobación previa del
            // alta la cruzan dos altas simultáneas juntas; el índice no (ADR-0051 §5).
            "ix_codigos_barras_gtin_uno_por_empresa (Unicidad) → codigo-barras-duplicado",
        ];

        Declaradas()
            .Select(par => $"{par.Key} ({par.Value.Clase}) → {par.Value.Error.Codigo}")
            .OrderBy(linea => linea, StringComparer.Ordinal)
            .ShouldBe(permitidas.OrderBy(linea => linea, StringComparer.Ordinal));
    }

    /// <summary>
    /// Ningún nombre está a la vez en esta lista y en la de la carrera perdida: el mismo
    /// <c>23505</c> no puede ser un <c>412</c> y un <c>422</c>.
    /// </summary>
    /// <remarks>
    /// Si estuviera en las dos, contestaría el manejador que va antes, el de la carrera, y la regla
    /// declarada no saldría nunca, sin que nada fallara. El barrido afirma primero que hay algo que
    /// cruzar: las dos listas con entradas, y un índice único entre las reglas.
    /// </remarks>
    [Fact]
    public void Ningun_nombre_es_a_la_vez_regla_y_carrera_perdida()
    {
        IReadOnlyDictionary<string, RestriccionDeclarada> reglas = Declaradas();
        IReadOnlyDictionary<string, string> carreras = _api.Services
            .GetRequiredService<IOptions<IndicesQueDelatanUnaCarreraPerdida>>().Value.Motivos;

        carreras.ShouldNotBeEmpty("sin carreras declaradas, el cruce sale vacío por no mirar nada");
        reglas.Values.ShouldContain(
            declarada => declarada.Clase == ClaseDeRestriccion.Unicidad,
            "sin un índice único entre las reglas, el cruce no puede encontrar nada");

        reglas.Keys.Intersect(carreras.Keys, StringComparer.Ordinal).ShouldBeEmpty(
            "estos nombres se traducen a la vez como regla y como carrera perdida");
    }

    private IReadOnlyDictionary<string, RestriccionDeclarada> Declaradas() =>
        _api.Services.GetRequiredService<IOptions<RestriccionesQueGuardanUnaRegla>>().Value.Declaradas;

    // Los nombres de las restricciones `CHECK` de todos los modelos, del de DISEÑO: el de ejecución
    // no las guarda. `Name` es el nombre en la base, el mismo que escribe la migración y el mismo
    // que devuelve PostgreSQL en `ConstraintName`.
    private HashSet<string> RestriccionesDelModelo()
    {
        HashSet<string> restricciones = new(StringComparer.Ordinal);

        foreach (IEntityType entidad in LosModelosDeCadaModulo.EntidadesDeDiseno(_api.Services))
        {
            foreach (ICheckConstraint restriccion in entidad.GetCheckConstraints())
            {
                if (!string.IsNullOrEmpty(restriccion.Name))
                {
                    restricciones.Add(restriccion.Name);
                }
            }
        }

        return restricciones;
    }

    // Los nombres de los índices ÚNICOS de todos los modelos, del mismo modelo de diseño. Uno que
    // deje de ser único sale de aquí, y su declaración se queda sin sitio.
    private HashSet<string> IndicesUnicosDelModelo()
    {
        HashSet<string> indices = new(StringComparer.Ordinal);

        foreach (IEntityType entidad in LosModelosDeCadaModulo.EntidadesDeDiseno(_api.Services))
        {
            foreach (IIndex indice in entidad.GetIndexes())
            {
                if (indice.IsUnique && indice.GetDatabaseName() is { Length: > 0 } nombre)
                {
                    indices.Add(nombre);
                }
            }
        }

        return indices;
    }
}
