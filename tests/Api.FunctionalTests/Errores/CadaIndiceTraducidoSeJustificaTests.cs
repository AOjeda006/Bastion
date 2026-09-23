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
/// Que la lista de índices traducidos a <c>412</c> y los índices que de verdad existen digan lo
/// mismo, <b>en los dos sentidos</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Una traducción por nombre se rompe en silencio.</b> El borde reconoce la carrera perdida por
/// el <c>ConstraintName</c> que trae la excepción de PostgreSQL, que es una cadena escrita a mano;
/// si una migración renombra el índice y nadie toca la declaración, no falla nada, no se registra
/// nada y la carrera vuelve a salir como <c>500</c> el día que ocurra —que es, por definición, un
/// día de mucho tráfico—. Este barrido es lo único que convierte ese silencio en un rojo.
/// </para>
/// <para>
/// <b>Y el otro sentido pesa más todavía.</b> Que el índice declarado siga siendo <b>único</b> es
/// la mitad que sostiene la garantía: si alguien le quita la unicidad «porque daba guerra», la
/// traducción se queda en pie traduciendo algo que ya no puede ocurrir, y el dato malo entra sin
/// que nadie conteste nada. Por eso no basta con que el índice exista: se exige único.
/// </para>
/// <para>
/// <b>Sin base de datos</b>: el modelo se construye antes de abrir ninguna conexión, así que esto
/// sale en el carril rápido y no en el de Testcontainers.
/// </para>
/// </remarks>
public sealed class CadaIndiceTraducidoSeJustificaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    /// <summary>Apaga la API levantada para este barrido.</summary>
    public void Dispose() => _api.Dispose();

    /// <summary>Todo índice declarado existe en el modelo y es único.</summary>
    [Fact]
    public void Todo_indice_declarado_existe_en_el_modelo_y_es_unico()
    {
        IReadOnlyDictionary<string, string> declarados = Declarados();

        declarados.ShouldNotBeEmpty(
            "sin una sola declaración, este barrido y el de abajo salen verdes por no haber " +
            "mirado nada (ADR-0020). El día que no quede ninguna, se borra el mecanismo entero");

        Dictionary<string, bool> delModelo = IndicesDelModelo();

        List<string> mal = [];

        foreach ((string indice, string motivo) in declarados)
        {
            if (!delModelo.TryGetValue(indice, out bool esUnico))
            {
                mal.Add($"«{indice}» está declarado y no existe en ningún modelo");
                continue;
            }

            if (!esUnico)
            {
                mal.Add($"«{indice}» está declarado y NO es único, así que su violación no puede " +
                        "ocurrir y la traducción sobra");
            }

            motivo.Trim().Length.ShouldBeGreaterThan(
                40, $"«{indice}» se declara sin decir por qué su violación no es un defecto");
        }

        mal.ShouldBeEmpty(
            "la lista de índices traducidos a 412 nombra índices que el modelo no tiene, o que " +
            "ya no son únicos");
    }

    /// <summary>
    /// Y ningún índice único del sistema se traduce por accidente: la lista es exactamente la que
    /// alguien decidió.
    /// </summary>
    /// <remarks>
    /// Este caso no compara contra el modelo sino contra una lista escrita <b>aquí</b>, que es lo
    /// que obliga a que añadir una traducción sea un acto deliberado en dos ficheros. Traducir un
    /// <c>23505</c> a <c>412</c> le dice al cliente «vuelve a leer y reintenta»; dicho de un índice
    /// que protege de un duplicado de verdad —dos altas con el mismo NIF—, es un bucle infinito
    /// con buenos modales.
    /// </remarks>
    [Fact]
    public void Ningun_indice_se_traduce_sin_estar_en_esta_lista()
    {
        // Los índices cuya violación NO es un defecto sino una carrera perdida. Uno por línea, y
        // cada uno con su motivo en el módulo que lo declara.
        string[] permitidos =
        [
            // Ítem 2.5: anular escribe el inverso y cambia el original, y el ORM decide cuál va
            // antes. Un `anula_a_id` repetido solo se escribe anulando dos veces el mismo
            // documento, así que no hay ningún otro desenlace posible que «otro llegó primero».
            "ix_ajustes_anula_a_id",
        ];

        Declarados().Keys.OrderBy(nombre => nombre, StringComparer.Ordinal)
            .ShouldBe(permitidos.OrderBy(nombre => nombre, StringComparer.Ordinal));
    }

    private IReadOnlyDictionary<string, string> Declarados() =>
        _api.Services.GetRequiredService<IOptions<IndicesQueDelatanUnaCarreraPerdida>>().Value.Motivos;

    // Nombre del índice -> si es único. El nombre que da EF Core aquí es el mismo que escribe la
    // migración y el mismo que devuelve PostgreSQL en `ConstraintName`, que es lo que hace que
    // comparar estas dos listas signifique algo.
    private Dictionary<string, bool> IndicesDelModelo()
    {
        Dictionary<string, bool> indices = new(StringComparer.Ordinal);

        foreach (IEntityType entidad in LosModelosDeCadaModulo.Entidades(_api.Services))
        {
            foreach (IIndex indice in entidad.GetIndexes())
            {
                string? nombre = indice.GetDatabaseName();

                if (!string.IsNullOrEmpty(nombre))
                {
                    indices[nombre] = indice.IsUnique;
                }
            }
        }

        return indices;
    }
}
