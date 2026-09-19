using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Numeracion;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Numeracion;

/// <summary>
/// El mecanismo de numeración <b>revienta</b> si nadie ha abierto una transacción antes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la guarda que no se nota sola, y por eso tiene caso propio.</b> EF Core abre una
/// transacción <b>implícita</b> por cada orden que ejecuta: sin esta comprobación, el incremento
/// del contador se confirmaría por su cuenta y todo parecería funcionar. El síntoma llegaría
/// mucho después y en otro sitio —un documento que falla al guardarse deja el número gastado, o
/// sea, un hueco—, y para entonces nadie lo relacionaría con esto.
/// </para>
/// <para>
/// <b>Se pregunta por <c>Database.CurrentTransaction</c> y no por una bandera propia</b>, porque
/// una bandera diría lo que el numerador cree y esto dice lo que EF Core tiene. El dueño de la
/// transacción es el filtro de idempotencia, que la abre cuando la acción declara la clave
/// obligatoria; una acción que se olvide de declararla llega aquí sin transacción, y este es el
/// único sitio donde eso se convierte en un fallo ruidoso en vez de en un hueco silencioso.
/// </para>
/// <para>
/// <b>Y no hay base de datos detrás.</b> La guarda corre antes de abrir ninguna conexión, así que
/// el caso vive en el carril rápido. Que el contexto de aquí no llegue a ninguna parte no es un
/// detalle del montaje: es lo que <see cref="El_contexto_de_este_caso_no_llega_a_ninguna_base"/>
/// afirma, para que «ha lanzado» no pueda confundirse con «no hay PostgreSQL».
/// </para>
/// </remarks>
public sealed class ElNumeradorExigeUnaTransaccionAbiertaTests
{
    [Fact]
    public async Task Numerar_sin_transaccion_abierta_revienta_en_vez_de_numerar()
    {
        await using ContextoQueNoLlegaANingunaParte contexto = new();
        NumeradorDeEsteCaso numerador = new(contexto, new InquilinoConEmpresa());

        contexto.Database.CurrentTransaction.ShouldBeNull();

        InvalidOperationException roto = await Should.ThrowAsync<InvalidOperationException>(
            () => numerador.TomarNumeroAsync(Guid.CreateVersion7(), CancellationToken.None));

        // El mensaje se afirma porque es la mitad útil de esta guarda: quien la encuentre en un
        // registro tiene que salir de ahí sabiendo QUÉ le falta a su acción, no solo que algo
        // estaba mal cableado.
        roto.Message.ShouldContain("No hay transacción abierta");
        roto.Message.ShouldContain("Idempotency-Key");
    }

    [Fact]
    public async Task El_contexto_de_este_caso_no_llega_a_ninguna_base()
    {
        // EL CANARIO DE LA GUARDA. Sin esto, el caso de arriba saldría verde igual si el numerador
        // no comprobara nada: bastaría con que la ejecución fallara por no haber PostgreSQL. Esto
        // enseña que ESE contexto, ejecutando SQL de verdad, falla de OTRA manera —con la
        // excepción del proveedor, no con la del numerador—, así que el `InvalidOperationException`
        // de arriba solo puede venir de la guarda.
        await using ContextoQueNoLlegaANingunaParte contexto = new();

        Exception roto = await Should.ThrowAsync<NpgsqlException>(
            () => contexto.Database.ExecuteSqlRawAsync("SELECT 1"));

        roto.ShouldNotBeOfType<InvalidOperationException>();
    }

    /// <summary>
    /// Un contexto con proveedor de verdad y un destino que no existe: sirve para preguntarle por
    /// su transacción, y revienta en cuanto alguien intenta usarlo de verdad.
    /// </summary>
    /// <remarks>
    /// El puerto 1 no lo escucha nadie, así que el intento de conexión se rechaza en el acto y el
    /// canario de arriba no paga ninguna espera. No es un contexto de mentira: es el proveedor de
    /// PostgreSQL entero, que es lo que hace que <c>CurrentTransaction</c> signifique lo mismo que
    /// en producción.
    /// </remarks>
    private sealed class ContextoQueNoLlegaANingunaParte : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder opciones) =>
            opciones.UseNpgsql("Host=127.0.0.1;Port=1;Database=ninguna;Username=nadie;Password=nada");
    }

    /// <summary>El numerador de verdad sobre el contexto de aquí. No cambia ni una línea de él.</summary>
    private sealed class NumeradorDeEsteCaso(DbContext contexto, IInquilinoActual inquilino)
        : NumeradorDeSerie(contexto, inquilino);

    private sealed class InquilinoConEmpresa : IInquilinoActual
    {
        public Guid? EmpresaDelFiltro => Guid.CreateVersion7();

        public bool HayEmpresaActiva => true;

        public MotivoSinInquilino? MotivoDelAmbito => null;

        public IDisposable SinInquilino(MotivoSinInquilino motivo) => throw new NotSupportedException();
    }
}
