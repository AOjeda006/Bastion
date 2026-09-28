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
/// <b>Sin base de datos</b>: el modelo se construye antes de abrir ninguna conexión, así que esto
/// sale en el carril rápido.
/// </para>
/// </remarks>
public sealed class CadaRestriccionTraducidaSeJustificaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    /// <summary>Apaga la API levantada para este barrido.</summary>
    public void Dispose() => _api.Dispose();

    /// <summary>Toda restricción declarada existe en el modelo, y dice por qué solo la guarda el motor.</summary>
    [Fact]
    public void Toda_restriccion_declarada_existe_en_el_modelo()
    {
        IReadOnlyDictionary<string, RestriccionDeclarada> declaradas = Declaradas();

        declaradas.ShouldNotBeEmpty(
            "sin una sola declaración, este barrido y el de abajo salen verdes por no haber " +
            "mirado nada (ADR-0020). El día que no quede ninguna, se borra el mecanismo entero");

        HashSet<string> delModelo = RestriccionesDelModelo();

        delModelo.ShouldNotBeEmpty(
            "el modelo no tiene ninguna restricción, así que el barrido no está leyendo el modelo");

        List<string> mal = [];

        foreach ((string restriccion, RestriccionDeclarada declarada) in declaradas)
        {
            if (!delModelo.Contains(restriccion))
            {
                mal.Add($"«{restriccion}» está declarada y no existe en ningún modelo");
            }

            declarada.Motivo.Trim().Length.ShouldBeGreaterThan(
                40, $"«{restriccion}» se declara sin decir por qué no se puede comprobar antes");
        }

        mal.ShouldBeEmpty(
            "la lista de restricciones traducidas nombra restricciones que el modelo no tiene");
    }

    /// <summary>
    /// Y ninguna restricción se traduce por accidente, ni con otro error: la lista, con su código,
    /// es la que está escrita aquí.
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
        // guardar, con el código con el que salen. Una por línea, con su motivo en el módulo.
        string[] permitidas =
        [
            // Ítem 2.8: el stock no baja de cero. Una comprobación previa la cruzan dos salidas
            // simultáneas juntas; el CHECK se evalúa con la fila ya bloqueada (ADR-0046 §4).
            "ck_existencias_fisico_no_negativo → stock-insuficiente",
        ];

        Declaradas()
            .Select(par => $"{par.Key} → {par.Value.Error.Codigo}")
            .OrderBy(linea => linea, StringComparer.Ordinal)
            .ShouldBe(permitidas.OrderBy(linea => linea, StringComparer.Ordinal));
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
}
