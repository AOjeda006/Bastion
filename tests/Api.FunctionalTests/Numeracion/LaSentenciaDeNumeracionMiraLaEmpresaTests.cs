using System.Reflection;
using System.Text.RegularExpressions;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Application.Numeracion;
using Bastion.BuildingBlocks.Infrastructure.Numeracion;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Pruebas.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Numeracion;

/// <summary>
/// La sentencia de numeración comprueba la empresa <b>ella misma</b>, y con el valor que sale del
/// inquilino.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la contrapartida de un salto del filtro global.</b> El SQL crudo no pasa por los filtros
/// de consulta de EF Core, así que la sentencia deja atrás la única defensa que impide que una
/// empresa lea o escriba filas de otra. <c>ElFiltroNoSeSaltaPorAhiTests</c> permite ese salto con
/// su argumento escrito; el argumento entero es que <b>aquí está la misma comparación</b>, a mano.
/// Este fichero es lo que impide que el argumento se quede sin base: si alguien quita la
/// condición, el salto sigue permitido —la lista no se entera— y el mecanismo pasa a numerar
/// series ajenas.
/// </para>
/// <para>
/// <b>Y el valor no puede venir de quien llama.</b> Una comprobación que compare contra un
/// identificador que llega en la petición no es una comprobación: es el mismo dato en los dos
/// lados de la igualdad. Por eso el puerto <b>no tiene parámetro de empresa</b> y el valor se
/// toma de <c>IInquilinoActual</c>, que es exactamente de donde lo toma el filtro global.
/// </para>
/// <para>
/// <b>Esto lee la cadena; que funcione lo dice el carril de integración</b>, con el caso que
/// confirma contra una serie de otra empresa y recibe un fallo. Los dos hacen falta: el de allí
/// abajo no distingue «no casó por la empresa» de «no casó por otra cosa», y el de aquí no sabe
/// si PostgreSQL entiende lo que lee.
/// </para>
/// </remarks>
public sealed class LaSentenciaDeNumeracionMiraLaEmpresaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void El_incremento_condiciona_por_la_empresa_de_la_serie_contra_un_parametro()
    {
        // La columna sale del modelo y no escrita otra vez, por lo mismo que en el fichero de al
        // lado: una cadena copiada no se entera de un renombrado.
        using IServiceScope alcance = _api.Services.CreateScope();
        IEntityType serie = alcance.ServiceProvider.GetRequiredService<OrganizacionDbContext>()
            .Model.FindEntityType(typeof(Serie))!;

        string empresa = serie.FindProperty(nameof(Serie.EmpresaId))!.GetColumnName(
            StoreObjectIdentifier.Create(serie, StoreObjectType.Table)!.Value)!;

        // Contra un PARÁMETRO, no contra un literal: un valor incrustado en la cadena sería una
        // empresa fija, que es peor que ninguna comprobación porque parece una.
        Match condicion = Regex.Match(
            NumeradorDeSerie.SqlDelIncremento,
            @"\bAND s\." + Regex.Escape(empresa) + @" = \{(\d+)\}",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        condicion.Success.ShouldBeTrue(
            "el incremento ya no compara la empresa de la serie contra un parámetro: " +
            NumeradorDeSerie.SqlDelIncremento);
    }

    [Fact]
    public void El_puerto_no_deja_que_quien_llama_elija_la_empresa()
    {
        // La ausencia de parámetro es la mitad estructural de la regla: mientras no se pueda pasar
        // una empresa, no hay forma de que la comparación se haga contra el dato de la petición.
        MethodInfo tomar = typeof(INumeradorDeSerie)
            .GetMethod(nameof(INumeradorDeSerie.TomarNumeroAsync))!;

        List<string> parametros = [.. tomar.GetParameters().Select(parametro => parametro.Name!)];

        parametros.ShouldBe(["serieId", "cancelacion"]);
    }

    [Fact]
    public void El_valor_que_compara_sale_del_inquilino_y_no_de_ningun_otro_sitio()
    {
        // ESTO SE LEE DEL FUENTE, y no hay otra manera de afirmarlo sin base de datos: que la
        // sentencia lleve un parámetro lo dice el caso de arriba, pero QUÉ se le pasa es una línea
        // de C#. Un `Guid` cualquiera casaría con la expresión de arriba igual de bien.
        string fuente = File.ReadAllText(Path.Combine(
            RaizDelRepositorio.Ruta(),
            "src/BuildingBlocks/Infrastructure/Numeracion/NumeradorDeSerie.cs"));

        // El barrido se afirma primero: un fichero movido dejaría todo lo de abajo buscando en una
        // cadena vacía, y `ShouldContain` sobre nada no se queja de nada.
        fuente.ShouldNotBeNullOrWhiteSpace();
        fuente.ShouldContain(nameof(NumeradorDeSerie.SqlDelIncremento));

        fuente.ShouldContain("inquilino.EmpresaDelFiltro");
        fuente.ShouldContain("[serieId, empresaId]");
    }

    [Fact]
    public void La_lectura_del_numero_no_repite_la_condicion_y_eso_es_a_proposito()
    {
        // NO ES UN OLVIDO, y queda afirmado para que nadie lo «arregle» sin entenderlo: la segunda
        // sentencia solo puede llegar a correr si la primera casó con una fila, o sea, si la serie
        // ya resultó ser de esta empresa y estar activa. Y lee la fila que la primera dejó
        // BLOQUEADA hasta el COMMIT, en la misma transacción, así que nadie ha podido moverla en
        // medio. Repetir aquí la condición obligaría a unir otra vez con `series` para leer un
        // número que ya está decidido.
        NumeradorDeSerie.SqlDelNumeroTomado.ShouldNotContain(NumeradorDeSerie.TablaDeSeries);
        NumeradorDeSerie.SqlDelIncremento.ShouldContain(NumeradorDeSerie.TablaDeSeries);
    }
}
