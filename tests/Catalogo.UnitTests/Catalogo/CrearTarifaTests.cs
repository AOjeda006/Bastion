using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.UnitTests.Dobles;
using Bastion.Organizacion.Contracts.Comun;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// El alta de un tramo de tarifa: la divisa por su estado, la vigencia y el solape.
/// </summary>
/// <remarks>
/// <para>
/// <b>LA DECISIÓN DE LA DIVISA CRUZADA, ejercida y no sólo escrita.</b> De las tres salidas
/// posibles —rechazar, aceptar y marcar, o convertir— se eligió <b>aceptar</b>, y convertir no es
/// de este ítem. Es la misma clase de decisión que la del <c>LimiteCredito</c> del 1.6 y la
/// respuesta es la contraria, con motivo: un límite de crédito <b>sin divisa</b> no era legítimo,
/// mientras que una tarifa de exportación en dólares es un caso real de una empresa que factura en
/// euros. Lo que hace que aceptarla no sea peligroso es que el precio resuelto viaja
/// <b>siempre</b> con su divisa pegada, y eso lo ejerce <c>ResolverPrecioTests</c>.
/// </para>
/// <para>
/// La forma en la que este fichero lo afirma es la única que no se puede fingir: el caso de uso
/// <b>no tiene por dónde</b> saber cuál es la divisa de la empresa —no recibe ese puerto— y a la
/// de la tarifa sólo le pregunta el ESTADO. Si algún día alguien añadiera la comparación,
/// tendría que añadir antes la dependencia.
/// </para>
/// </remarks>
public sealed class CrearTarifaTests
{
    private static readonly Guid s_empresa = Guid.NewGuid();
    private static readonly DateTimeOffset s_ahora = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly s_hoy = new(2026, 9, 11);

    [Fact]
    public async Task Una_tarifa_en_otra_divisa_se_acepta_y_a_la_divisa_solo_se_le_pregunta_el_estado()
    {
        DivisasEn divisas = new(EstadoDeMaestro.SeOfreceParaLoNuevo);
        TarifasEnMemoria tarifas = new();
        var enDolares = Guid.NewGuid();

        Resultado<TarifaDto> resultado = await Crear(tarifas, divisas).EjecutarAsync(
            Peticion(divisaId: enDolares), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue(
            "una tarifa de exportación en otra divisa es un caso real y se acepta a propósito");

        resultado.Valor.DivisaId.ShouldBe(enDolares);

        divisas.Preguntadas.ShouldBe(
            [enDolares],
            "a la divisa se le pregunta UNA cosa —en qué estado está— y sólo por la de la " +
            "tarifa. Comparar con la de la empresa exigiría un puerto que este caso de uso no " +
            "tiene, que es lo que hace que la decisión de aceptarla no se pueda revertir por " +
            "descuido");
    }

    [Fact]
    public async Task Una_divisa_retirada_no_se_ofrece_para_una_tarifa_nueva()
    {
        // La otra mitad del ADR-0023: retirada sigue resolviendo lo viejo y no se ofrece para lo
        // nuevo. Abrir hoy una tarifa en una divisa que ya no se usa es exactamente lo nuevo.
        DivisasEn divisas = new(EstadoDeMaestro.SoloResuelveLoViejo);
        TarifasEnMemoria tarifas = new();

        Resultado<TarifaDto> resultado = await Crear(tarifas, divisas).EjecutarAsync(
            Peticion(), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Tipo.ShouldBe(TipoDeError.Conflicto);
        resultado.Error.Codigo.ShouldBe("tarifa-divisa-retirada");
        tarifas.Guardadas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Una_divisa_que_no_existe_tampoco()
    {
        DivisasEn divisas = new(EstadoDeMaestro.NoExiste);
        TarifasEnMemoria tarifas = new();

        Resultado<TarifaDto> resultado = await Crear(tarifas, divisas).EjecutarAsync(
            Peticion(), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();

        // `Validacion` y no `NoEncontrado`, que es el criterio del 1.7 y del 1.8 y no se reabre
        // aquí: lo que no se encuentra no es el recurso de la ruta —la tarifa todavía no existe—,
        // es un identificador que venía en el CUERPO, y eso es un 400, no un 404.
        resultado.Error!.Tipo.ShouldBe(TipoDeError.Validacion);

        // Lo que de verdad los separa es el `type`, y por eso se comprueba aquí al lado del caso
        // de la retirada: «no existe» se arregla escribiendo bien el identificador y «está
        // retirada» eligiendo otra divisa. Son dos arreglos distintos.
        resultado.Error.Codigo.ShouldBe("tarifa-divisa-no-encontrada");
    }

    [Fact]
    public async Task Una_vigencia_al_reves_se_contesta_antes_de_preguntar_nada()
    {
        // Un tramo que acaba antes de empezar es una petición mal escrita, no un conflicto con lo
        // guardado. Contestarla aquí es lo que evita que el dominio tenga que lanzar por algo que
        // llega de fuera (ADR-0004), y que se gaste un viaje preguntando por la divisa.
        DivisasEn divisas = new(EstadoDeMaestro.SeOfreceParaLoNuevo);
        TarifasEnMemoria tarifas = new();

        Resultado<TarifaDto> resultado = await Crear(tarifas, divisas).EjecutarAsync(
            Peticion() with { VigenteHasta = s_hoy.AddDays(-1) },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("tarifa-vigencia-al-reves");
        divisas.Preguntadas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dos_tramos_del_mismo_codigo_que_se_pisan_no_entran()
    {
        // Aquí se contesta con un error con nombre; quien de verdad lo impide es la restricción de
        // exclusión de la base, que es la única que cubre dos peticiones a la vez. Es la mutación
        // 6: sin ella, esta comprobación previa deja pasar el solape entre dos altas simultáneas.
        DivisasEn divisas = new(EstadoDeMaestro.SeOfreceParaLoNuevo);
        TarifasEnMemoria tarifas = new() { DiceQueHaySolape = true };

        Resultado<TarifaDto> resultado = await Crear(tarifas, divisas).EjecutarAsync(
            Peticion(), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("tarifa-vigencias-solapadas");
        resultado.Error.Tipo.ShouldBe(TipoDeError.Conflicto);
    }

    [Fact]
    public async Task El_codigo_se_normaliza_a_mayusculas_antes_de_guardarlo()
    {
        DivisasEn divisas = new(EstadoDeMaestro.SeOfreceParaLoNuevo);
        TarifasEnMemoria tarifas = new();

        Resultado<TarifaDto> resultado = await Crear(tarifas, divisas).EjecutarAsync(
            Peticion() with { Codigo = "  pvp  " }, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.Codigo.ShouldBe("PVP");
    }

    private static CrearTarifaDto Peticion(Guid? divisaId = null) => new()
    {
        Codigo = "PVP",
        Nombre = "Precio de venta al público",
        DivisaId = divisaId ?? Guid.NewGuid(),
        VigenteDesde = s_hoy,
    };

    private static CrearTarifa Crear(TarifasEnMemoria tarifas, DivisasEn divisas) =>
        new(new UsuarioDe(s_empresa),
            tarifas,
            new EmpresasQueContestan(activa: true),
            divisas,
            new ConfirmacionesContadas(),
            new RelojParado(s_ahora));
}
