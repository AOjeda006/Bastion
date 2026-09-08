using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.UnitTests.Dobles;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// Las dos puertas por las que se escribe el padre de una categoría comprueban el árbol, y la
/// segunda es la que importa.
/// </summary>
/// <remarks>
/// <para>
/// <c>ElArbolSigueSiendoUnArbolTests</c> prueba la comprobación; este fichero prueba que
/// <b>alguien la llama</b>, y desde los dos sitios. No es lo mismo, y la diferencia es
/// exactamente la mutación 3: borrar la llamada de <c>ModificarCategoria</c> deja intacta la
/// comprobación, verde su fichero de tests, y el sistema sin ninguna protección contra ciclos —
/// porque el alta, que sí la conserva, no puede cerrar ninguno.
/// </para>
/// <para>
/// En el alta la rama del ciclo es <b>inalcanzable por construcción</b>: la categoría todavía no
/// existe, así que ninguna cadena de padres puede volver a ella. Lo que la llamada del alta sí
/// hace es lo otro que la comprobación afirma —que el padre existe y que la cadena no se pasa de
/// honda—, y por eso se llama igualmente.
/// </para>
/// </remarks>
public sealed class ElCicloSeCompruebaEnLasDosPuertasTests
{
    private static readonly Guid s_empresa = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTimeOffset s_momento =
        new(2026, 9, 8, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Mover_una_rama_debajo_de_su_propia_descendencia_se_rechaza()
    {
        // FERR -> TORN, y se pide mover FERR debajo de TORN. Este es EL caso: el único camino por
        // el que un ciclo puede entrar por la API.
        CategoriasEnMemoria arbol = new();
        var ferreteria = Categoria.Crear(s_empresa, "FERR", "Ferretería", null, s_momento);
        var tornillos = Categoria.Crear(s_empresa, "TORN", "Tornillos", ferreteria.Id, s_momento);

        arbol.Guardadas.Add(ferreteria);
        arbol.Guardadas.Add(tornillos);
        arbol.Con(ferreteria.Id, null, "FERR").Con(tornillos.Id, ferreteria.Id, "TORN");

        ConfirmacionesContadas confirmaciones = new();

        var modificar = new ModificarCategoria(arbol, confirmaciones, new VersionesQueDanIgual());

        Resultado<CategoriaDto> resultado = await modificar.EjecutarAsync(
            ferreteria.Id,
            new VersionDeRecurso(1),
            new ModificarCategoriaDto { Nombre = "Ferretería", PadreId = tornillos.Id },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse(
            "la comprobación de ciclos tiene que correr también en la modificación: es la única " +
            "operación que puede cerrar uno");
        resultado.Error!.Codigo.ShouldBe("categoria-ciclo");

        // Y no se ha guardado nada: el rechazo es antes del `Modificar`, no después.
        ferreteria.PadreId.ShouldBeNull();
        confirmaciones.Veces.ShouldBe(0);
    }

    [Fact]
    public async Task Una_categoria_no_puede_pasar_a_colgar_de_si_misma()
    {
        CategoriasEnMemoria arbol = new();
        var ferreteria = Categoria.Crear(s_empresa, "FERR", "Ferretería", null, s_momento);

        arbol.Guardadas.Add(ferreteria);
        arbol.Con(ferreteria.Id, null, "FERR");

        var modificar = new ModificarCategoria(
            arbol, new ConfirmacionesContadas(), new VersionesQueDanIgual());

        Resultado<CategoriaDto> resultado = await modificar.EjecutarAsync(
            ferreteria.Id,
            new VersionDeRecurso(1),
            new ModificarCategoriaDto { Nombre = "Ferretería", PadreId = ferreteria.Id },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("categoria-ciclo");
    }

    [Fact]
    public async Task Mover_una_rama_a_un_sitio_legitimo_se_admite()
    {
        // La contrapartida, sin la cual «se rechaza» podría ser «se rechaza siempre». TORN pasa a
        // colgar de MATE, que no es descendencia suya.
        CategoriasEnMemoria arbol = new();
        var ferreteria = Categoria.Crear(s_empresa, "FERR", "Ferretería", null, s_momento);
        var material = Categoria.Crear(s_empresa, "MATE", "Material", null, s_momento);
        var tornillos = Categoria.Crear(s_empresa, "TORN", "Tornillos", ferreteria.Id, s_momento);

        arbol.Guardadas.Add(tornillos);
        arbol.Con(ferreteria.Id, null, "FERR")
            .Con(material.Id, null, "MATE")
            .Con(tornillos.Id, ferreteria.Id, "TORN");

        ConfirmacionesContadas confirmaciones = new();

        var modificar = new ModificarCategoria(arbol, confirmaciones, new VersionesQueDanIgual());

        Resultado<CategoriaDto> resultado = await modificar.EjecutarAsync(
            tornillos.Id,
            new VersionDeRecurso(1),
            new ModificarCategoriaDto { Nombre = "Tornillos", PadreId = material.Id },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        tornillos.PadreId.ShouldBe(material.Id);
        confirmaciones.Veces.ShouldBe(1);
    }

    [Fact]
    public async Task El_alta_bajo_un_padre_que_no_existe_se_rechaza()
    {
        // Lo que la llamada del alta SÍ afirma. La rama del ciclo es inalcanzable ahí, pero las
        // otras dos no, y sin la llamada una categoría nacería colgada de la nada.
        CategoriasEnMemoria arbol = new();

        var crear = new CrearCategoria(
            new UsuarioDe(s_empresa),
            arbol,
            new EmpresasQueContestan(activa: true),
            new ConfirmacionesContadas(),
            new RelojParado(s_momento));

        Resultado<CategoriaDto> resultado = await crear.EjecutarAsync(
            new CrearCategoriaDto
            {
                Codigo = "TORN",
                Nombre = "Tornillos",
                PadreId = Guid.NewGuid(),
            },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("categoria-padre-no-encontrado");
        arbol.Guardadas.ShouldBeEmpty();
    }

    [Fact]
    public async Task El_alta_demasiado_honda_se_rechaza_tambien()
    {
        CategoriasEnMemoria arbol = new();
        IReadOnlyList<Guid> cadena = arbol.ConCadenaDe(Categoria.ProfundidadMaxima + 1);

        var crear = new CrearCategoria(
            new UsuarioDe(s_empresa),
            arbol,
            new EmpresasQueContestan(activa: true),
            new ConfirmacionesContadas(),
            new RelojParado(s_momento));

        Resultado<CategoriaDto> resultado = await crear.EjecutarAsync(
            new CrearCategoriaDto
            {
                Codigo = "HOND",
                Nombre = "Demasiado honda",
                PadreId = cadena[^1],
            },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("categoria-demasiado-profunda");
    }

    [Fact]
    public async Task El_alta_de_una_raiz_pasa()
    {
        CategoriasEnMemoria arbol = new();
        ConfirmacionesContadas confirmaciones = new();

        var crear = new CrearCategoria(
            new UsuarioDe(s_empresa),
            arbol,
            new EmpresasQueContestan(activa: true),
            confirmaciones,
            new RelojParado(s_momento));

        Resultado<CategoriaDto> resultado = await crear.EjecutarAsync(
            new CrearCategoriaDto { Codigo = "ferr", Nombre = "Ferretería", PadreId = null },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.Codigo.ShouldBe("FERR", "el código se normaliza antes de guardarse");
        arbol.Guardadas.Count.ShouldBe(1);
        confirmaciones.Veces.ShouldBe(1);
    }
}
