using System.Text.RegularExpressions;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Application.Ejercicios;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Bastion.Pruebas.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Ejercicios;

/// <summary>
/// Las dos sentencias del cerrojo del ejercicio comprueban la empresa <b>ellas mismas</b>, cada
/// una por su lado, y con el valor que sale del inquilino.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la cláusula 4 del criterio del ADR-0040</b>, y lo que la tanda de mutaciones del 2.6
/// encontró sin cubrir. Quitada la comparación de la sentencia de Organización, <b>ningún caso de
/// ningún carril</b> se puso rojo. Quitada la de Inventario se pusieron dieciocho, y ninguno por
/// diseño: caían en la guarda de «más de una fila», porque el carril de integración comparte la
/// base entre cientos de empresas con ejercicio del año en curso. Con una sola empresa en la base,
/// verde. Un rojo que depende de cuántos vecinos haya no es una defensa.
/// </para>
/// <para>
/// <b>Por eso va una por sentencia y no una sobre las dos juntas.</b>
/// <c>LaSentenciaDelEjercicioNombraLaTablaDeVerdadTests</c> extrae las columnas de las dos cadenas
/// concatenadas, y <c>e.empresa_id</c> sale igual aunque una de las dos la haya perdido: la otra
/// la sigue nombrando. Es la misma trampa que un barrido sobre la unión de dos conjuntos.
/// </para>
/// <para>
/// <b>Y el valor no puede venir de quien llama.</b> Ninguno de los dos puertos tiene parámetro de
/// empresa, y el valor se toma de <c>IInquilinoActual</c> —de donde lo toma el filtro global—.
/// Comparar contra un identificador de la petición sería poner el mismo dato a los dos lados de la
/// igualdad.
/// </para>
/// <para>
/// <b>Esto lee las cadenas; que funcione lo dice el carril de integración</b>, con
/// <c>ElEjercicioRigeElAjusteTests.Cerrar_el_ejercicio_cerrado_de_otra_empresa_es_el_mismo_404_que_uno_inventado</c>.
/// Hacen falta los dos, por lo mismo que en la numeración: aquel no distingue «no casó por la
/// empresa» de «no casó por otra cosa», y éste no sabe si PostgreSQL entiende lo que lee.
/// </para>
/// </remarks>
public sealed class LaSentenciaDelEjercicioMiraLaEmpresaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void El_puerto_de_Inventario_compara_la_empresa_contra_el_segundo_parametro()
    {
        // `{1}` y no un parámetro cualquiera: la llamada pasa `fecha, empresaId`, en ese orden, y
        // un `{0}` compararía la empresa contra la FECHA. Que la llamada siga pasándolos así lo
        // afirma el último caso de este fichero.
        LaComparacionDeEmpresa(LosEjerciciosDesdeInventario.SqlDelEstadoConCerrojo)
            .ShouldBe("1", "el puerto de Inventario ya no compara la empresa contra `{1}`: " +
                LosEjerciciosDesdeInventario.SqlDelEstadoConCerrojo);
    }

    [Fact]
    public void El_cerrojo_de_Organizacion_compara_la_empresa_contra_el_segundo_parametro()
    {
        // Éste es el que ninguna prueba cazaba. El identificador del ejercicio llega de la ruta,
        // así que sin esta comparación el cerrojo encuentra la fila ajena, la bloquea en exclusiva
        // y devuelve su estado ANTES de que la lectura por el ORM —la que sí lleva el filtro—
        // tenga ocasión de decir que no existe.
        LaComparacionDeEmpresa(CerrojoDeEjercicios.SqlDelEstadoConCerrojo)
            .ShouldBe("1", "el cerrojo de Organización ya no compara la empresa contra `{1}`: " +
                CerrojoDeEjercicios.SqlDelEstadoConCerrojo);
    }

    [Fact]
    public void Ninguno_de_los_dos_puertos_deja_que_quien_llama_elija_la_empresa()
    {
        // La mitad estructural: mientras no se pueda pasar una empresa, la comparación no se
        // puede hacer contra el dato de la petición.
        Parametros(typeof(IConsultaDeEjercicios), nameof(IConsultaDeEjercicios.ParaEscribirEnAsync))
            .ShouldBe(["fecha", "cancelacion"]);

        Parametros(typeof(ICerrojoDeEjercicios), nameof(ICerrojoDeEjercicios.TomarEnExclusivaAsync))
            .ShouldBe(["id", "cancelacion"]);
    }

    [Theory]
    [InlineData(
        "src/Modules/Inventario/Bastion.Inventario.Infrastructure/Persistencia/Repositorios/LosEjerciciosDesdeInventario.cs",
        "SqlQueryRaw<string>(SqlDelEstadoConCerrojo, fecha, empresaId)")]
    [InlineData(
        "src/Modules/Organizacion/Bastion.Organizacion.Infrastructure/Persistencia/Repositorios/CerrojoDeEjercicios.cs",
        "SqlQueryRaw<string>(SqlDelEstadoConCerrojo, id, empresaId)")]
    public void El_valor_que_comparan_sale_del_inquilino_y_va_en_su_sitio(
        string fichero, string llamada)
    {
        // SE LEE DEL FUENTE, como en la numeración: que la cadena lleve `{1}` lo dicen los casos
        // de arriba, pero QUÉ va en esa posición es una línea de C#, y un `Guid` cualquiera
        // casaría con la expresión igual de bien.
        string fuente = File.ReadAllText(Path.Combine(RaizDelRepositorio.Ruta(), fichero));

        // El barrido se afirma primero: un fichero movido dejaría lo de abajo buscando en una
        // cadena vacía.
        fuente.ShouldNotBeNullOrWhiteSpace();
        fuente.ShouldContain("SqlDelEstadoConCerrojo");

        fuente.ShouldContain("Guid empresaId = inquilino.EmpresaDelFiltro");
        fuente.ShouldContain(llamada);
    }

    private string LaComparacionDeEmpresa(string sentencia)
    {
        // La columna sale del modelo y no escrita otra vez: una cadena copiada no se entera de un
        // renombrado.
        using IServiceScope alcance = _api.Services.CreateScope();
        IEntityType ejercicio = alcance.ServiceProvider.GetRequiredService<OrganizacionDbContext>()
            .Model.FindEntityType(typeof(Ejercicio))!;

        string empresa = ejercicio.FindProperty(nameof(Ejercicio.EmpresaId))!.GetColumnName(
            StoreObjectIdentifier.Create(ejercicio, StoreObjectType.Table)!.Value)!;

        // Contra un PARÁMETRO y no contra un literal: una empresa incrustada en la cadena sería una
        // empresa fija, que es peor que ninguna comprobación porque parece una.
        Match condicion = Regex.Match(
            sentencia,
            @"\b(?:WHERE|AND) e\." + Regex.Escape(empresa) + @" = \{(\d+)\}",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        return condicion.Success ? condicion.Groups[1].Value : "(ninguna)";
    }

    private static List<string> Parametros(Type puerto, string metodo) =>
        [.. puerto.GetMethod(metodo)!.GetParameters().Select(parametro => parametro.Name!)];
}
