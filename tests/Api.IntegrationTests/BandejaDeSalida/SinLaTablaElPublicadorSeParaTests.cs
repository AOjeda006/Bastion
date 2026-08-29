using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// Qué hace el trabajo de fondo cuando la tabla que tiene que vaciar <b>no está</b>.
/// </summary>
/// <remarks>
/// <para>
/// No es un caso teórico: el <c>docker-compose</c> de desarrollo levanta la base vacía y nadie
/// aplica las migraciones —es el riesgo que quedó abierto y que se cierra en el 0.13—. Hasta
/// entonces, lo que se decide aquí es qué pasa mientras: el publicador <b>se para y lo dice una
/// vez</b>, en vez de escribir un error por vuelta desde el arranque hasta que alguien apague el
/// contenedor. Un registro con dos errores por segundo no es información: es el sitio donde se
/// esconden los errores de verdad.
/// </para>
/// <para>
/// <b>Los dos caminos hasta la misma tabla que falta.</b> Contra una base recién creada no falta
/// solo la tabla: falta el esquema entero. Y aun así PostgreSQL contesta lo mismo a las dos
/// —«undefined_table», 42P01—, porque un <c>SELECT</c> sobre un esquema que no existe es una
/// relación que no existe. Los dos casos están aquí para que eso quede fijado: si algún día
/// dejaran de responder igual, el primero se pondría rojo.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido; de él sale a qué servidor conectarse.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class SinLaTablaElPublicadorSeParaTests(PostgresConTodosLosModulos postgres)
{
    private const int NoEstaLaTabla = 8303;

    private const int LaVueltaFallo = 8302;

    [Fact]
    public async Task Contra_una_base_sin_migrar_se_para_y_lo_dice_una_sola_vez()
    {
        // Sin esquema, que es como se encuentra una base a la que nadie ha aplicado nada.
        string cadena = await BaseNuevaAsync(conEsquema: false);

        await using BandejaDeVerdad bandeja = new(cadena, publica: true);

        await bandeja.ArrancarAsync();

        (await BandejaDeVerdad.EsperarAsync(
            () => Task.FromResult(bandeja.Registro.Veces(NoEstaLaTabla) > 0)))
            .ShouldBeTrue("el publicador no ha avisado de que la tabla no está");

        // Y a partir de ahí, silencio: se para. Si siguiera dando vueltas, en este medio segundo
        // habría escrito varias líneas más — que es justo el ruido que esto evita.
        await Task.Delay(500);

        bandeja.Registro.Veces(NoEstaLaTabla).ShouldBe(1, "se para: no avisa una vez por vuelta");
        bandeja.Registro.Veces(LaVueltaFallo).ShouldBe(0, "esto no es una vuelta fallida, es una decisión");
    }

    [Fact]
    public async Task Y_con_el_esquema_puesto_pero_sin_la_tabla_hace_lo_mismo()
    {
        // El otro camino hasta la misma decisión: alguien creó el esquema —o lo creó otro módulo—
        // y las migraciones de la bandeja no están aplicadas.
        string cadena = await BaseNuevaAsync(conEsquema: true);

        await using BandejaDeVerdad bandeja = new(cadena, publica: true);

        await bandeja.ArrancarAsync();

        (await BandejaDeVerdad.EsperarAsync(
            () => Task.FromResult(bandeja.Registro.Veces(NoEstaLaTabla) > 0)))
            .ShouldBeTrue("con el esquema pero sin la tabla, el publicador tampoco avisa");

        await Task.Delay(500);

        bandeja.Registro.Veces(NoEstaLaTabla).ShouldBe(1);
    }

    // Una base sin migrar, y —para el segundo caso— con el esquema creado a mano. No se toca la
    // compartida: la de los demás tests tiene las migraciones aplicadas, que es lo contrario de lo
    // que hace falta aquí.
    private async Task<string> BaseNuevaAsync(bool conEsquema)
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: false);

        if (conEsquema)
        {
            await using NpgsqlConnection nueva = new(cadena);
            await nueva.OpenAsync();

            await using NpgsqlCommand esquema = new(
                $"CREATE SCHEMA \"{ConfiguracionDeLaBandeja.Esquema}\"", nueva);
            await esquema.ExecuteNonQueryAsync();
        }

        return cadena;
    }
}
