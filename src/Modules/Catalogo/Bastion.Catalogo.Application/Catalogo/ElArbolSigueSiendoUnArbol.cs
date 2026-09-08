using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>
/// Colgar una categoría de otra no puede cerrar un ciclo, ni hacer el árbol más hondo que
/// <see cref="Categoria.ProfundidadMaxima"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>El hueco que cierra.</b> Una lista de adyacencia no es un árbol: es un grafo donde cada nodo
/// apunta a uno. Nada en el motor —ni un <c>NOT NULL</c>, ni una clave ajena a la propia tabla—
/// impide que <c>A</c> cuelgue de <c>B</c>, <c>B</c> de <c>C</c> y <c>C</c> de <c>A</c>. Con eso
/// guardado, las tres categorías dejan de tener raíz: no salen en ningún recorrido desde arriba,
/// y cualquier ascenso desde una de ellas <b>no termina</b>. La precedencia de tarifas del ítem
/// 1.9 —artículo, categoría más cercana, … , raíz— es precisamente un ascenso.
/// </para>
/// <para>
/// <b>Vive en la capa de aplicación y no en el dominio</b>, exactamente como
/// <c>LaInversaEsPlausible</c> del ítem 1.7 — el patrón se reutiliza, no se inventa otro. La regla
/// relaciona <b>varias instancias</b> del agregado, y la R12 dice una transacción, un agregado: un
/// invariante de dominio que tuviera que cargar el resto del árbol sería justo la grieta que la
/// R12 cierra. Tampoco es una restricción de la base, que tendría que recorrer otras filas y por
/// tanto ser un disparador recursivo, con el coste y la invisibilidad que tienen. El fallo es un
/// error de negocio con nombre, no una excepción (ADR-0004).
/// </para>
/// <para>
/// <b>Se comprueba en el alta y en la modificación, y lo segundo es donde está el hueco de
/// verdad.</b> En el alta, la rama del ciclo es <b>estructuralmente inalcanzable</b> y conviene
/// decirlo en voz alta en vez de fingir que protege algo: la categoría que nace todavía no está en
/// el árbol, así que ningún antepasado suyo puede ser ella. Lo que el alta sí ejerce son las otras
/// dos ramas —que el padre exista, y que colgar de él no pase de la profundidad máxima—, y ésas no
/// son vacuas ni de lejos. El ciclo lo cierra <b>reasignar el padre</b> de una rama debajo de su
/// propia descendencia, que solo se puede hacer modificando; una comprobación que únicamente
/// mirase el alta dejaría pasar el único camino por el que se rompe, y lo dejaría pasar en verde.
/// Es la misma forma del hueco que el ítem 1.7 encontró en la inversa, por su otra cara.
/// </para>
/// <para>
/// <b>El caso degenerado —padre igual a sí misma— no tiene tratamiento aparte</b>, y es una
/// decisión: es un ciclo de longitud uno, sale por la primera vuelta del mismo recorrido y con el
/// mismo error con nombre. Para quien lo provoca es el mismo problema, y una rama suya solo daría
/// un sitio más donde equivocarse.
/// </para>
/// </remarks>
internal static class ElArbolSigueSiendoUnArbol
{
    /// <summary>Código del error con el que se rechaza un ciclo.</summary>
    public const string CodigoDeCiclo = "categoria-ciclo";

    /// <summary>Código del error de un padre que no existe.</summary>
    public const string CodigoDePadreNoEncontrado = "categoria-padre-no-encontrado";

    /// <summary>Código del error de un árbol que pasaría de la profundidad máxima.</summary>
    public const string CodigoDeDemasiadaProfundidad = "categoria-demasiado-profunda";

    /// <summary>
    /// Comprueba que colgar esa categoría de ese padre deja el árbol siendo un árbol.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El recorrido está acotado, y la cota no es una precaución: es lo que convierte un cuelgue
    /// en un error.</b> Sobre datos que ya tuvieran un ciclo —una restauración a medias, un
    /// <c>UPDATE</c> a mano—, un ascenso sin cota no da error, <b>gira</b>, dentro de una petición
    /// y con la conexión abierta. Un servidor que no contesta es peor que cualquier rechazo, porque
    /// no dice de qué venía. La cota está en <see cref="Categoria.ProfundidadMaxima"/> con su
    /// motivo escrito, y se lee de allí en vez de repetirse aquí.
    /// </para>
    /// <para>
    /// El recorrido gasta <b>una consulta por nivel</b>, así que la cota acota también el coste:
    /// como mucho once viajes a la base por alta o modificación con padre. Es la otra mitad de por
    /// qué el número es diez y no mil.
    /// </para>
    /// </remarks>
    /// <param name="categoriaId">
    /// La categoría que se cuelga, o <see cref="Guid.Empty"/> en un alta —donde todavía no tiene
    /// identidad en el árbol y la rama del ciclo no puede darse—.
    /// </param>
    /// <param name="padreId">De quién va a colgar, o nulo para dejarla como raíz.</param>
    /// <param name="categorias">Por dónde se leen los eslabones del ascenso.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    internal static async Task<Resultado> ComprobarAsync(
        Guid categoriaId,
        Guid? padreId,
        IRepositorioDeCategorias categorias,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(categorias);

        // Sin padre no hay nada que comprobar: una raíz no cuelga de nadie, no cierra ningún ciclo
        // y su profundidad es cero.
        if (padreId is not { } primero)
        {
            return Resultado.Correcto();
        }

        // Los códigos de la cadena, para poder contarla en el mensaje. Un «hay un ciclo» a secas
        // obliga a quien lo lee a reconstruir a mano por dónde va; con la cadena escrita, se ve.
        var camino = new List<string>();
        Guid? actual = primero;

        // `ProfundidadMaxima + 1` vueltas: cada antepasado visitado es un nivel de profundidad de
        // la categoría que se cuelga, así que visitar uno más que el máximo YA es pasarse.
        for (int nivel = 0; nivel <= Categoria.ProfundidadMaxima; nivel++)
        {
            if (actual is not { } id)
            {
                // Se ha llegado a una raíz sin encontrarse a sí misma: es un árbol.
                return Resultado.Correcto();
            }

            // La primera vuelta cubre el caso degenerado —padre igual a sí misma— sin una rama
            // propia: es un ciclo de longitud uno.
            if (id == categoriaId)
            {
                return Resultado.Fallo(ErrorDeOperacion.Conflicto(
                    CodigoDeCiclo,
                    "Esa categoría no puede colgar de ahí: se acabaría clasificando dentro de sí " +
                    $"misma. La cadena de padres vuelve a ella pasando por {Cadena(camino)}. Un " +
                    "árbol de clasificación tiene que poder recorrerse hasta una raíz."));
            }

            EslabonDeCategoria? eslabon = await categorias
                .EslabonAsync(id, cancelacion)
                .ConfigureAwait(false);

            if (eslabon is null)
            {
                // Un padre que no existe se cuenta como validación y no como conflicto: quien lo
                // escribió tiene que corregir el identificador, no elegir otro sitio del árbol. Y
                // vale igual para «no existe» que para «es de otra empresa»: la consulta lleva el
                // filtro de inquilinato puesto, así que una categoría prestada no se ve — que es
                // lo que hace que R8 no tenga aquí una rama propia donde poder olvidarse.
                return Resultado.Fallo(ErrorDeOperacion.Validacion(
                    CodigoDePadreNoEncontrado,
                    $"No hay ninguna categoría con el identificador {id} en esta empresa."));
            }

            camino.Add(eslabon.Codigo);
            actual = eslabon.PadreId;
        }

        return Resultado.Fallo(ErrorDeOperacion.Conflicto(
            CodigoDeDemasiadaProfundidad,
            $"El árbol de categorías admite {Categoria.ProfundidadMaxima} niveles por debajo de " +
            $"la raíz, y colgar ahí pasaría de esa cuenta: {Cadena(camino)}. A esa profundidad lo " +
            "que suele haber no es una clasificación, es un atributo del artículo disfrazado de " +
            "categoría. Y si la cadena no llega a ninguna raíz, lo que hay guardado es un ciclo."));
    }

    // `List<string>` y no `IReadOnlyList<string>` porque lo exige CA1859, que en esta solución
    // es error: el único que llama pasa la lista concreta, y la interfaz solo añadiría un salto
    // por despacho virtual sin desacoplar nada —el método es privado—.
    private static string Cadena(List<string> codigos) =>
        codigos.Count == 0 ? "(ninguna)" : string.Join(" → ", codigos);
}
