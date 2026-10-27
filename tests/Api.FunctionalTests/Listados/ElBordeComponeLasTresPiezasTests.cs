using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Listados;

/// <summary>
/// Cómo compone el borde las tres piezas de un listado —la página, el orden con sus criterios y el
/// texto— y cuándo rechaza el texto (ADR-0072).
/// </summary>
/// <remarks>
/// <para>
/// <b>En el carril rápido</b>, porque la decisión no necesita base: es la cadena de consulta contra
/// lo que la acción enlazó. El barrido de integración,
/// <c>ElTextoSoloLoRecibeQuienLoMiraTests</c>, la ejerce por HTTP en los 23 listados, detrás de un
/// contenedor; esto la ejerce sin él, que es donde tiene que poder ponerse roja con Docker parado.
/// </para>
/// <para>
/// <b>Nulo es «este listado no tiene texto»</b>, y no «no han mandado texto». Lo que se rechaza es
/// un <c>?q=</c> con algo escrito que llega a un listado con el texto nulo; en blanco, o sin
/// <c>?q=</c>, pasa, porque es lo que manda un formulario con la caja vacía.
/// </para>
/// </remarks>
public sealed class ElBordeComponeLasTresPiezasTests
{
    private static readonly PaginaPedida s_segunda = new() { Pagina = 2, Tamanio = 5 };

    [Fact]
    public void Un_texto_escrito_a_un_listado_sin_texto_es_un_400_con_su_codigo()
    {
        Resultado<Paginacion> pedido = Componer("?q=zz", texto: null);

        pedido.EsCorrecto.ShouldBeFalse("el texto ha llegado a un listado que no lo mira y ha pasado");
        pedido.Error!.Codigo.ShouldBe("filtro-no-admitido");
        pedido.Error.Tipo.ShouldBe(TipoDeError.Validacion);
        pedido.Error.Campos.Keys.ShouldBe(["q"]);
    }

    /// <summary>
    /// Las parejas del de arriba: el mismo listado sin <c>?q=</c> o con él en blanco, y el mismo
    /// <c>?q=</c> en un listado que sí lo mira.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("?q=")]
    [InlineData("?q=%20%20")]
    [InlineData("?page=2&size=5")]
    public void Sin_texto_escrito_un_listado_sin_texto_compone_la_pagina(string consulta)
    {
        Resultado<Paginacion> pedido = Componer(consulta, texto: null);

        pedido.EsCorrecto.ShouldBeTrue(pedido.Error?.Mensaje);
        pedido.Valor.Filtro.ShouldBeNull();
        pedido.Valor.Pagina.ShouldBe(2, "la página es la pieza enlazada, no la cadena de consulta");
        pedido.Valor.Tamanio.ShouldBe(5);
    }

    [Fact]
    public void Un_listado_con_texto_lo_compone_con_la_pagina_y_el_orden()
    {
        Resultado<Paginacion> pedido = Componer(
            "?q=zz&sort=-codigo", new FiltroDeTexto { Texto = "zz" }, new ConsultaDeListado { Orden = "-codigo" });

        pedido.EsCorrecto.ShouldBeTrue(pedido.Error?.Mensaje);
        pedido.Valor.Filtro.ShouldBe("zz");
        pedido.Valor.Orden.ShouldBe(new Orden("codigo", Descendente: true));
        pedido.Valor.Pagina.ShouldBe(2);
        pedido.Valor.Tamanio.ShouldBe(5);
    }

    /// <summary>
    /// El orden se sigue rechazando con su propio código, y el texto que no se admite va antes:
    /// los dos son un <c>400</c>, y el del texto es el que dice qué sobra.
    /// </summary>
    [Fact]
    public void Un_orden_que_no_se_admite_sigue_siendo_su_400()
    {
        Componer("?sort=nombre", texto: null, new ConsultaDeListado { Orden = "nombre" })
            .Error!.Codigo.ShouldBe(ConsultaDeListado.CodigoDeOrdenNoAdmitido);

        Componer("?q=zz&sort=nombre", texto: null, new ConsultaDeListado { Orden = "nombre" })
            .Error!.Codigo.ShouldBe(FiltroDeTexto.CodigoDeFiltroNoAdmitido);
    }

    private static Resultado<Paginacion> Componer(
        string consulta,
        FiltroDeTexto? texto,
        ConsultaDeListado? criterios = null)
    {
        DefaultHttpContext contexto = new();
        contexto.Request.QueryString = new QueryString(consulta.Length == 0 ? null : consulta);

        Controlador controlador = new() { ControllerContext = new ControllerContext { HttpContext = contexto } };

        return RespuestasDeListado.Componer(
            controlador, s_segunda, criterios ?? new ConsultaDeListado(), texto, new PorCodigo());
    }

    private sealed class Controlador : ControllerBase;

    private sealed class PorCodigo : IOrdenaPor
    {
        public IReadOnlySet<string> CamposOrdenables { get; } = new HashSet<string>(StringComparer.Ordinal) { "codigo" };
    }
}
