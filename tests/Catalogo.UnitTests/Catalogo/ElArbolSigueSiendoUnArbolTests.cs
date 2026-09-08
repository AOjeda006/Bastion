using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.UnitTests.Dobles;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// Que el árbol de categorías siga siendo un árbol: la comprobación de ciclos, su caso degenerado
/// y su cota.
/// </summary>
/// <remarks>
/// <para>
/// <b>El hueco está en la modificación, no en el alta.</b> Una categoría que nace no puede cerrar
/// un ciclo: todavía no tiene descendencia de la que colgarse. El ciclo se cierra <b>moviendo</b>
/// una rama debajo de su propia descendencia, y eso solo pasa al modificar. Comprobarlo únicamente
/// en el alta es la forma más natural de escribirlo y la que no protege de nada — es exactamente
/// la mutación 3.
/// </para>
/// <para>
/// <b>Y se comprueba contra el árbol tal como está</b>, leyéndolo por el puerto, no contra el que
/// el llamante creyera tener. Por eso esto vive en la capa de aplicación y no en el agregado: una
/// categoría no puede decidir sola si el sitio al que la mueven cuelga de ella, porque para
/// saberlo hace falta el resto del árbol.
/// </para>
/// </remarks>
public sealed class ElArbolSigueSiendoUnArbolTests
{
    [Fact]
    public async Task Sin_padre_es_una_raiz_y_no_hay_nada_que_recorrer()
    {
        CategoriasEnMemoria arbol = new();

        Resultado resultado = await ElArbolSigueSiendoUnArbol.ComprobarAsync(
            Guid.NewGuid(), null, arbol, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        arbol.Llamadas.ShouldBe(0, "una raíz no obliga a preguntar por ningún eslabón");
    }

    [Fact]
    public async Task Una_categoria_no_puede_ser_su_propia_madre()
    {
        // El caso degenerado: un ciclo de longitud UNO. No tiene rama propia en el código a
        // propósito —la primera vuelta del recorrido ya lo caza—, y por eso hace falta el caso:
        // sin él, nadie sabría si esa primera vuelta existe.
        var ella = Guid.NewGuid();
        CategoriasEnMemoria arbol = new();
        arbol.Con(ella, null, "FERR");

        Resultado resultado = await ElArbolSigueSiendoUnArbol.ComprobarAsync(
            ella, ella, arbol, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("categoria-ciclo");
        resultado.Error.Tipo.ShouldBe(TipoDeError.Conflicto);

        // Y se caza SIN leer el árbol: el identificador que llega ya es el suyo.
        arbol.Llamadas.ShouldBe(0);
    }

    [Fact]
    public async Task Una_categoria_no_puede_colgar_de_su_propia_descendencia()
    {
        // El ciclo de verdad, el de dos eslabones: FERR -> TORN, y ahora se quiere mover FERR
        // debajo de TORN. La base de datos no puede verlo —su CHECK solo mira una fila— y la
        // clave ajena tampoco: FERR existe y TORN existe.
        var ferreteria = Guid.NewGuid();
        var tornillos = Guid.NewGuid();

        CategoriasEnMemoria arbol = new();
        arbol.Con(ferreteria, null, "FERR").Con(tornillos, ferreteria, "TORN");

        Resultado resultado = await ElArbolSigueSiendoUnArbol.ComprobarAsync(
            ferreteria, tornillos, arbol, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("categoria-ciclo");

        // El mensaje dice POR DÓNDE vuelve la cadena. Un «hay un ciclo» a secas obliga a quien lo
        // lee a reconstruirlo a mano sobre un árbol que puede tener cientos de ramas.
        resultado.Error.Mensaje.ShouldContain("TORN");
    }

    [Fact]
    public async Task Un_padre_que_no_existe_es_validacion_y_no_conflicto()
    {
        CategoriasEnMemoria arbol = new();

        Resultado resultado = await ElArbolSigueSiendoUnArbol.ComprobarAsync(
            Guid.NewGuid(), Guid.NewGuid(), arbol, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("categoria-padre-no-encontrado");

        // Validación: hay que corregir el identificador, no elegir otro sitio del árbol. Y vale
        // igual para «no existe» que para «es de otra empresa», porque la consulta lleva puesto el
        // filtro de inquilinato y una categoría prestada no se ve.
        resultado.Error.Tipo.ShouldBe(TipoDeError.Validacion);
    }

    [Fact]
    public async Task Colgar_en_el_ultimo_nivel_que_cabe_se_admite()
    {
        // La frontera por el lado bueno, que es la mitad que se olvida: una cota que rechazara uno
        // de menos también saldría verde en el caso de arriba.
        CategoriasEnMemoria arbol = new();
        IReadOnlyList<Guid> cadena = arbol.ConCadenaDe(Categoria.ProfundidadMaxima);

        Resultado resultado = await ElArbolSigueSiendoUnArbol.ComprobarAsync(
            Guid.NewGuid(), cadena[^1], arbol, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue(
            $"colgar de una cadena de {Categoria.ProfundidadMaxima} llega justo a la cota y no " +
            "la pasa");
    }

    [Fact]
    public async Task Colgar_un_nivel_mas_abajo_se_rechaza_por_profundidad()
    {
        CategoriasEnMemoria arbol = new();
        IReadOnlyList<Guid> cadena = arbol.ConCadenaDe(Categoria.ProfundidadMaxima + 1);

        Resultado resultado = await ElArbolSigueSiendoUnArbol.ComprobarAsync(
            Guid.NewGuid(), cadena[^1], arbol, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("categoria-demasiado-profunda");
    }

    /// <summary>
    /// La cota, ejercida sobre datos que ya tienen un ciclo: el recorrido termina, y lo hace
    /// deprisa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Este es el caso de la mutación 5, y está escrito para que FALLE, no para que se cuelgue.</b>
    /// Sin cota, un ascenso sobre una cadena que ya es cíclica en la base de datos no da error: da
    /// vueltas para siempre, y en producción eso es una petición que no termina y una conexión que
    /// no se suelta. En una suite de tests es peor todavía, porque el síntoma es «la CI tarda» y
    /// eso no señala a nadie.
    /// </para>
    /// <para>
    /// Lo que lo convierte en un caso normal es el tope del doble: a las cien preguntas lanza con
    /// un mensaje que dice qué ha pasado. Con la cota puesta, el recorrido para en once y el tope
    /// no llega a tocarse nunca.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Un_ciclo_YA_GUARDADO_no_deja_el_recorrido_dando_vueltas()
    {
        // Dos categorías que se apuntan la una a la otra. No se puede llegar a esto por la API
        // —de eso van los casos de arriba—, pero sí con un `UPDATE` a mano, una importación o una
        // migración de datos de otro sistema. La comprobación tiene que sobrevivir a leerlo.
        var una = Guid.NewGuid();
        var otra = Guid.NewGuid();

        CategoriasEnMemoria arbol = new();
        arbol.Con(una, otra, "UNA").Con(otra, una, "OTRA");

        Resultado resultado = await ElArbolSigueSiendoUnArbol.ComprobarAsync(
            Guid.NewGuid(), una, arbol, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("categoria-demasiado-profunda");

        arbol.Llamadas.ShouldBeLessThanOrEqualTo(
            Categoria.ProfundidadMaxima + 1,
            "el recorrido tiene que parar en la cota. Si esta cuenta se dispara, lo que hay es un " +
            "ascenso sin límite sobre datos cíclicos");
    }

    [Fact]
    public void La_cota_esta_escrita_y_es_la_del_dominio()
    {
        // El número no vive en el caso de uso: vive en `Categoria`, junto a su motivo, porque es
        // una decisión sobre lo que un árbol de clasificación puede ser y no sobre cómo se
        // recorre. Este caso lo ata para que nadie lo duplique en la aplicación.
        Categoria.ProfundidadMaxima.ShouldBe(10);
    }
}
