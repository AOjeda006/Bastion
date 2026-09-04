using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Composicion;

/// <summary>
/// Todo lo que <c>AgregarInquilinato()</c> registra se puede <b>construir</b> sin llamar a ningún
/// otro método de extensión del proyecto.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta regla existe porque su promesa se rompió.</b> El comentario de <c>AgregarInquilinato</c>
/// dice, desde el 0.9, que el inquilinato y el acceso a lo bloqueado van juntos «porque los dos son
/// cosas que un <c>DbContext</c> de módulo necesita para construirse, y separarlos dejaría un host
/// que registra una y se olvida de la otra reventando al resolver el primer contexto». En el ítem
/// 1.4, <c>AccesoALoBloqueado</c> estrenó una dependencia —<c>IUsuarioActual</c>, para que la traza
/// del art. 32 diga <b>quién</b> pregunta— que se registraba en OTRO método de extensión
/// (<c>AgregarAutorizacionPorPermisos</c>). El host completo llama a los dos y no notó nada; el del
/// publicador de la bandeja, que corre fuera de una petición y no necesita permisos, llama solo al
/// primero, y se cayó con «Unable to resolve service for type 'IUsuarioActual'» en <b>diez</b> casos
/// del carril de integración. El paquete había dejado de ser un paquete sin que nada lo dijera.
/// </para>
/// <para>
/// <b>Por qué en el carril rápido y no donde se cayó.</b> Aquello se vio en integración, con Docker,
/// varios minutos después de empujar; esto es una colección de servicios en memoria y tarda
/// milisegundos. Un fallo de composición no necesita una base de datos para verse: necesita que
/// alguien intente construir lo registrado, que es exactamente lo que nadie hacía.
/// </para>
/// <para>
/// <b>La lista de qué construir se DESCUBRE, no se teclea.</b> Se mira qué descriptores añade la
/// llamada y se resuelven todos: un servicio nuevo entra en la regla solo, el día que se registra, y
/// no el día que alguien se acuerde de añadirlo aquí.
/// </para>
/// <para>
/// <b>Lo único que se le concede es el registro.</b> <c>AddLogging()</c> lo tiene cualquier host de
/// .NET por construcción —lo mete el <c>WebApplicationBuilder</c> antes que nada—, así que exigirlo
/// no es exigir un método NUESTRO. La raya está ahí: lo que no puede hacer falta es otra extensión
/// de este proyecto.
/// </para>
/// </remarks>
public sealed class ElInquilinatoSeConstruyeSoloTests
{
    [Fact]
    public void Todo_lo_que_registra_el_inquilinato_se_puede_construir_sin_nada_mas()
    {
        ServiceCollection servicios = [];
        servicios.AddLogging();

        int antes = servicios.Count;

        servicios.AgregarInquilinato();

        List<Type> registrados =
        [
            .. servicios.Skip(antes)
                .Select(descriptor => descriptor.ServiceType)
                .Distinct(),
        ];

        // El arnés de la regla (ADR-0020). Si un día `AgregarInquilinato` dejara de registrar nada
        // —renombrado, movido, vaciado— el bucle de abajo no recorrería nada y esto saldría verde
        // sin haber construido un solo servicio.
        registrados.ShouldNotBeEmpty(
            "`AgregarInquilinato()` no ha añadido ni un descriptor, así que este barrido no está " +
            "construyendo nada y su silencio no significa nada");

        // `validateScopes` porque todo esto es `scoped`: sin él, resolver desde la raíz pasaría y
        // escondería justo el error que un host de verdad se comería en la primera petición.
        using ServiceProvider proveedor = servicios.BuildServiceProvider(validateScopes: true);
        using IServiceScope ambito = proveedor.CreateScope();

        List<string> rotos = [];

        foreach (Type servicio in registrados)
        {
            try
            {
                ambito.ServiceProvider.GetRequiredService(servicio).ShouldNotBeNull();
            }
            catch (InvalidOperationException fallo)
            {
                rotos.Add($"{servicio.Name}: {fallo.Message}");
            }
        }

        rotos.ShouldBeEmpty(
            "`AgregarInquilinato()` registra servicios que no se pueden construir con lo que ese " +
            "mismo método deja puesto, así que un host que lo llame sin llamar además a otra " +
            "extensión de este proyecto revienta al resolverlos —y el que revienta suele ser el " +
            "trabajo de fondo, que es el que menos se mira—: " + string.Join(" | ", rotos));
    }

    [Fact]
    public void Y_el_acceso_a_lo_bloqueado_se_construye_nombrandolo()
    {
        // La pregunta de control de la de arriba, y no una duplicación: aquella recorre lo que
        // ENCUENTRE, y si el descubrimiento se rompiera recorrería una lista vacía —la afirmación
        // de no vaciedad lo diría— o una lista que ya no incluye este servicio, y saldría verde.
        // Esta nombra el servicio del que se sabe que la promesa se rompió, para que el día que se
        // salga de este paquete se lea aquí y no en un rojo del carril de integración.
        ServiceCollection servicios = [];
        servicios.AddLogging();
        servicios.AgregarInquilinato();

        using ServiceProvider proveedor = servicios.BuildServiceProvider(validateScopes: true);
        using IServiceScope ambito = proveedor.CreateScope();

        ambito.ServiceProvider.GetRequiredService<IAccesoALoBloqueado>().ShouldNotBeNull();
    }

    [Fact]
    public void Y_llamarlo_dos_veces_no_registra_nada_dos_veces()
    {
        // `TryAdd` no es un detalle de estilo: el host completo llama a esta extensión Y a
        // `AgregarAutorizacionPorPermisos`, y las dos registran `IUsuarioActual`. Con `Add` a
        // secas habría dos descriptores para el mismo servicio, y quien resuelve se queda con el
        // último — o sea que el orden de las llamadas del `Program.cs` decidiría la
        // implementación, en silencio.
        ServiceCollection servicios = [];
        servicios.AddLogging();
        servicios.AgregarInquilinato();

        int antes = servicios.Count;
        servicios.AgregarInquilinato();

        servicios.Count.ShouldBe(
            antes,
            "llamar dos veces a `AgregarInquilinato()` añade descriptores, así que ha dejado de " +
            "usar `TryAdd` y el orden de registro del host pasa a decidir qué implementación gana");
    }
}
