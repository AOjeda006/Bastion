using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Numeracion;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Numeracion;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Numeracion;

/// <summary>
/// El cerrojo de la numeración contra PostgreSQL de verdad: lo que solo el motor puede demostrar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esto no se puede probar sin base de datos, y no por comodidad.</b> Lo que el mecanismo
/// promete es que dos confirmaciones simultáneas <b>no</b> se llevan el mismo número, y quien lo
/// impide es el cerrojo de fila que PostgreSQL toma al ejecutar el incremento. Un doble no tiene
/// cerrojos: cualquier imitación en memoria daría verde sin haber ejercido lo único que importa.
/// </para>
/// <para>
/// <b>El caso de la simultaneidad afirma primero que hay espera.</b> Sin eso sería vacío: dos
/// llamadas una detrás de otra dan 1 y 2 aunque no haya cerrojo ninguno, así que un mecanismo que
/// leyera y sumara —el que este diseño existe para evitar— saldría verde igual. Lo que se afirma
/// es que la segunda <b>sigue sin contestar</b> mientras la primera no confirma, y eso solo pasa
/// si hay una fila bloqueada de por medio.
/// </para>
/// <para>
/// <b>La otra mitad es que deshacer devuelva el número</b>, y ahí se ve por qué la R5 no admite una
/// secuencia de PostgreSQL: <c>nextval</c> no se revierte, así que una confirmación que falla
/// dejaría el número gastado y el hueco sería permanente. El contador es una fila y vuelve atrás
/// con su transacción.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido, con las migraciones aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElCerrojoDeLaNumeracionTests(PostgresConTodosLosModulos postgres)
{
    // Cuánto se espera para afirmar que la segunda numeración SIGUE bloqueada. La afirmación es de
    // un solo sentido y por eso el plazo no la vuelve frágil: si no hubiera cerrojo, la segunda
    // contestaría en milisegundos y el caso saldría rojo por mucho que se alargara esto; y si lo
    // hay, no contesta por mucho que se acorte.
    private static readonly TimeSpan s_esperaParaVerElBloqueo = TimeSpan.FromMilliseconds(750);

    [Fact]
    public async Task Dos_numeraciones_simultaneas_se_llevan_numeros_distintos_y_consecutivos()
    {
        (Guid empresa, Guid serie) = await UnaSerieNuevaAsync(Escenario.NifInventado(301), TipoDeDocumento.FacturaEmitida);

        await using OrganizacionDbContext primero = postgres.AbrirOrganizacion(empresa);
        await using OrganizacionDbContext segundo = postgres.AbrirOrganizacion(empresa);

        await using IDbContextTransaction unaTransaccion = await primero.Database.BeginTransactionAsync();
        await using IDbContextTransaction laOtra = await segundo.Database.BeginTransactionAsync();

        Resultado<long> uno = await Numerador(primero, empresa).TomarNumeroAsync(serie, CancellationToken.None);
        uno.EsCorrecto.ShouldBeTrue();

        // La segunda arranca SIN esperar: la llamada se queda pendiente dentro del motor, porque la
        // fila del contador está bloqueada hasta que la primera transacción confirme.
        Task<Resultado<long>> pendiente = Numerador(segundo, empresa)
            .TomarNumeroAsync(serie, CancellationToken.None);

        Task ganadora = await Task.WhenAny(pendiente, Task.Delay(s_esperaParaVerElBloqueo));

        ganadora.ShouldNotBe(
            pendiente,
            "la segunda numeración ha contestado sin esperar a que la primera confirme: no hay " +
            "cerrojo, y el resto de este caso saldría verde igual con dos llamadas en fila");

        await unaTransaccion.CommitAsync();

        Resultado<long> dos = await pendiente;
        await laOtra.CommitAsync();

        dos.EsCorrecto.ShouldBeTrue();
        new[] { uno.Valor, dos.Valor }.ShouldBe([1L, 2L]);
    }

    [Fact]
    public async Task Deshacer_la_transaccion_devuelve_el_numero_y_el_siguiente_lo_reutiliza()
    {
        // AQUÍ ESTÁ LA DIFERENCIA ENTERA CON UNA SECUENCIA. `nextval` no se revierte: una
        // confirmación que falla después de pedir el número lo dejaría gastado para siempre, y
        // eso es un hueco -lo que el artículo 6.1.a del RD 1619/2012 no admite-. El contador es
        // una fila, y una fila vuelve atrás con su transacción.
        (Guid empresa, Guid serie) = await UnaSerieNuevaAsync(Escenario.NifInventado(302), TipoDeDocumento.AlbaranDeVenta);

        await using (OrganizacionDbContext contexto = postgres.AbrirOrganizacion(empresa))
        {
            await using IDbContextTransaction transaccion = await contexto.Database.BeginTransactionAsync();

            Resultado<long> tomado = await Numerador(contexto, empresa)
                .TomarNumeroAsync(serie, CancellationToken.None);

            tomado.EsCorrecto.ShouldBeTrue();
            tomado.Valor.ShouldBe(1);

            await transaccion.RollbackAsync();
        }

        // Se lee desde un contexto NUEVO: el de arriba tiene la serie en su rastreador, y leerla
        // de ahí contestaría lo que el proceso creyó, no lo que quedó en la base.
        (await ContadorDeAsync(empresa, serie)).ShouldBe(0);

        await using (OrganizacionDbContext otro = postgres.AbrirOrganizacion(empresa))
        {
            Resultado<long> siguiente = await NumeradorDePruebas.NumerarAsync(otro, empresa, serie);

            siguiente.EsCorrecto.ShouldBeTrue();
            siguiente.Valor.ShouldBe(1, "el número que se deshizo tiene que volver a salir: si " +
                "saliera el 2, el 1 sería un hueco que nadie podría explicar");
        }
    }

    [Fact]
    public async Task Una_serie_cerrada_no_numera()
    {
        (Guid empresa, Guid serie) = await UnaSerieNuevaAsync(Escenario.NifInventado(303), TipoDeDocumento.FacturaEmitida);

        await using (OrganizacionDbContext contexto = postgres.AbrirOrganizacion(empresa))
        {
            Serie suya = await contexto.Series.SingleAsync(fila => fila.Id == serie);
            suya.Cerrar();
            await contexto.SaveChangesAsync();
        }

        await using OrganizacionDbContext otro = postgres.AbrirOrganizacion(empresa);

        Resultado<long> numero = await NumeradorDePruebas.NumerarAsync(otro, empresa, serie);

        // EL ESTADO VIAJA EN EL `WHERE`, no en una guarda de C#, y esta es la única manera de
        // comprobarlo: la guarda que se fue con `Serie.RegistrarNumeroAsignado` la sostenía el
        // dominio, y la de ahora la sostiene la cláusula que condiciona el incremento.
        numero.EsCorrecto.ShouldBeFalse();
        numero.Error!.Codigo.ShouldBe(ErroresDeNumeracion.CodigoDeSerieNoNumera);
        (await ContadorDeAsync(empresa, serie)).ShouldBe(0);
    }

    [Fact]
    public async Task Una_serie_de_otra_empresa_no_numera()
    {
        (Guid ajena, Guid serie) = await UnaSerieNuevaAsync(Escenario.NifInventado(304), TipoDeDocumento.FacturaEmitida);
        (Guid propia, _) = await UnaSerieNuevaAsync(Escenario.NifInventado(305), TipoDeDocumento.FacturaEmitida);

        await using OrganizacionDbContext contexto = postgres.AbrirOrganizacion(propia);

        // El identificador de la serie de otra sociedad, con el inquilino de la mía. Es el caso
        // que el SQL crudo deja sin filtro global: la única defensa es la comparación que la
        // propia sentencia lleva escrita.
        Resultado<long> numero = await NumeradorDePruebas.NumerarAsync(contexto, propia, serie);

        numero.EsCorrecto.ShouldBeFalse();
        numero.Error!.Codigo.ShouldBe(ErroresDeNumeracion.CodigoDeSerieNoNumera);

        // Y el contador de la ajena sigue donde estaba: no basta con que conteste mal, tiene que
        // no haber tocado nada.
        (await ContadorDeAsync(ajena, serie)).ShouldBe(0);
    }

    [Fact]
    public async Task Una_serie_que_no_existe_da_el_MISMO_error_que_una_ajena()
    {
        (Guid empresa, _) = await UnaSerieNuevaAsync(Escenario.NifInventado(306), TipoDeDocumento.FacturaEmitida);

        await using OrganizacionDbContext contexto = postgres.AbrirOrganizacion(empresa);

        Resultado<long> numero = await NumeradorDePruebas.NumerarAsync(
            contexto, empresa, Guid.CreateVersion7());

        // MISMO CÓDIGO A PROPÓSITO, y el caso existe para que siga siéndolo. Si «no existe» y «es
        // de otra empresa» contestaran distinto, confirmar un documento sería un oráculo: probando
        // identificadores se sabría cuáles son series de otra sociedad. Es el mismo criterio con
        // el que se decidió el alta de un tercero.
        numero.EsCorrecto.ShouldBeFalse();
        numero.Error!.Codigo.ShouldBe(ErroresDeNumeracion.CodigoDeSerieNoNumera);
    }

    [Fact]
    public async Task Suprimir_pierde_contra_una_numeracion_que_se_cuela_entre_la_lectura_y_el_borrado()
    {
        // ESTA CARRERA CAMBIÓ DE GUARDIÁN EN EL ADR-0039, y por eso tiene caso propio desde hoy.
        // Antes la sostenía el `xmin` de `series`: numerar movía esa fila, y el `If-Match` que
        // `EliminarSerie` exige se quedaba viejo solo. Con el contador fuera, numerar ya NO toca
        // la fila de la serie: el `ETag` de quien tenía la ficha abierta sigue valiendo -que es
        // justo lo que se buscaba- y esta carrera se quedaría sin nadie si no fuera porque el
        // borrado se lleva por delante la FILA DEL CONTADOR, con el testigo de ESA fila dentro.
        //
        // Lo que se ejerce es la ventana de una sola petición: `EliminarSerie` lee la serie con su
        // contador, pregunta `SePuedeSuprimir` y guarda. Si entre la lectura y el guardado alguien
        // numera, la comprobación de C# ya dijo que sí y solo queda el testigo.
        (Guid empresa, Guid serie) = await UnaSerieNuevaAsync(Escenario.NifInventado(307), TipoDeDocumento.PedidoDeVenta);

        await using OrganizacionDbContext queSuprime = postgres.AbrirOrganizacion(empresa);

        Serie leida = await queSuprime.Series.SingleAsync(fila => fila.Id == serie);
        leida.SePuedeSuprimir.ShouldBeTrue("el caso empieza con una serie que SÍ se podía borrar");

        // La confirmación de otro, que entra y sale entera.
        await using (OrganizacionDbContext queNumera = postgres.AbrirOrganizacion(empresa))
        {
            (await NumeradorDePruebas.NumerarAsync(queNumera, empresa, serie))
                .EsCorrecto.ShouldBeTrue();
        }

        queSuprime.Series.Remove(leida);

        // El borde traduce esto a un 412 para todas las acciones a la vez
        // (`ManejadorDeVersionObsoleta`), así que lo que hay que afirmar aquí es el choque.
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => queSuprime.SaveChangesAsync());

        // Y NO SE HA BORRADO NADA. Un choque que dejara el borrado a medias -la serie fuera y su
        // contador dentro, o al revés- sería peor que el choque.
        (await ContadorDeAsync(empresa, serie)).ShouldBe(1);
    }

    [Fact]
    public async Task Borrar_una_serie_a_mano_sin_su_contador_sigue_siendo_imposible()
    {
        // LA CONTRAPARTIDA DEL APLAZAMIENTO, y por eso este caso va pegado al de arriba. Para que
        // el testigo del ORM pueda hablar primero, la clave ajena pasó de `RESTRICT` a `NO ACTION`
        // aplazada: `RESTRICT` no se puede aplazar en PostgreSQL -esa es la diferencia entera
        // entre las dos-, y mientras comprobaba en el acto ganaba siempre, convirtiendo la carrera
        // en un `23503` que el borde no sabe traducir.
        //
        // Lo que NO cambia es la prohibición: un contador huérfano sigue siendo imposible. Cambia
        // CUÁNDO se comprueba -al confirmar, no al ejecutar-, y eso es justo lo que este caso
        // enseña: la sentencia pasa, y la transacción no.
        (Guid empresa, Guid serie) = await UnaSerieNuevaAsync(Escenario.NifInventado(308), TipoDeDocumento.PedidoDeCompra);

        await using OrganizacionDbContext contexto = postgres.AbrirOrganizacion(empresa);
        await using IDbContextTransaction transaccion = await contexto.Database.BeginTransactionAsync();

        // Por la puerta de atrás y a mano: el ORM nunca borraría la serie sin su contador. Esto es
        // lo que quedaría si alguien escribiera esa sentencia -en una migración, en un arreglo de
        // madrugada- y la clave ajena fuera la única defensa.
        await contexto.Database.ExecuteSqlRawAsync(
            "DELETE FROM " + NumeradorDeSerie.Esquema + "." + NumeradorDeSerie.TablaDeSeries +
            " WHERE id = {0}", serie);

        await Should.ThrowAsync<PostgresException>(() => transaccion.CommitAsync());

        // Y la serie sigue entera: lo que se deshace es la transacción, no media tabla.
        (await ContadorDeAsync(empresa, serie)).ShouldBe(0);
    }

    /// <summary>
    /// Los tipos de documento sobre los que se ejerce la numeración: <b>todos</b>, descubiertos.
    /// </summary>
    /// <remarks>
    /// El recorrido se saca del enumerado y no se escribe: el día que entre un documento nuevo
    /// —la transferencia y el recuento ya están puestos para el 2.11 y el 2.12— su serie se
    /// numera aquí sin que nadie se acuerde de añadirlo. Una lista a mano se habría quedado en los
    /// seis de la fase 1.
    /// </remarks>
    public static TheoryData<TipoDeDocumento> LosTiposDeDocumento()
    {
        TheoryData<TipoDeDocumento> tipos = [];

        foreach (TipoDeDocumento tipo in Enum.GetValues<TipoDeDocumento>())
        {
            tipos.Add(tipo);
        }

        return tipos;
    }

    [Theory]
    [MemberData(nameof(LosTiposDeDocumento))]
    public async Task Una_serie_numera_sin_huecos_sea_cual_sea_el_documento_que_numera(TipoDeDocumento tipo)
    {
        // EL MECANISMO NO SABE QUÉ NUMERA, y este caso lo afirma en vez de darlo por hecho. La
        // sentencia condiciona por estado y empresa, no por tipo, así que una factura y un ajuste
        // de inventario pasan por exactamente el mismo cerrojo. Si algún día alguien metiera una
        // condición por tipo -por ejemplo, para que los documentos internos numeren «más
        // barato»-, el tipo que se quedara fuera saldría rojo aquí y no en producción.
        (Guid empresa, Guid serie) = await UnaSerieNuevaAsync(Escenario.NifInventado(320 + (int)tipo), tipo);

        List<long> numeros = [];

        await using (OrganizacionDbContext contexto = postgres.AbrirOrganizacion(empresa))
        {
            for (int vuelta = 0; vuelta < 4; vuelta++)
            {
                Resultado<long> numero = await NumeradorDePruebas.NumerarAsync(contexto, empresa, serie);

                numero.EsCorrecto.ShouldBeTrue($"{tipo} no ha numerado en la vuelta {vuelta}");
                numeros.Add(numero.Valor);
            }
        }

        // SIN HUECOS Y CORRELATIVA, que son dos de las tres cláusulas de R5 y se afirman juntas
        // porque juntas se rompen: una lista sin repetidos pero con saltos cumple la primera y no
        // la segunda, y la comparación entera caza las dos.
        numeros.ShouldBe([1L, 2L, 3L, 4L]);
        (await ContadorDeAsync(empresa, serie)).ShouldBe(4);
    }

    [Fact]
    public void El_recorrido_de_arriba_no_se_deja_ningun_tipo_de_documento()
    {
        // La afirmación del ADR-0020 para un `[Theory]`: si el recorrido se quedara vacío -o
        // alguien lo cambiara por una lista escrita a mano-, el caso de arriba no se ejecutaría
        // ni una vez y la suite saldría verde, más rápida, sin haber numerado nada.
        List<TipoDeDocumento> recorridos =
            [.. ((IEnumerable<object[]>)LosTiposDeDocumento())
                .Select(fila => (TipoDeDocumento)fila[0])];

        recorridos.ShouldNotBeEmpty();
        recorridos.ShouldBe(Enum.GetValues<TipoDeDocumento>(), ignoreOrder: true);
    }

    // El numerador de verdad sobre el contexto que se le dé. No sustituye nada del mecanismo:
    // solo permite quedarse a mitad de una transacción, que es lo que el caso del cerrojo
    // necesita y `NumerarAsync` -que abre y cierra la suya- no deja hacer.
    private static NumeradorDePruebas Numerador(DbContext contexto, Guid empresa) =>
        new(contexto, new InquilinoFijo(empresa));

    // Una empresa con su ejercicio y una serie activa a cero, por la puerta de atrás: montar esto
    // por la API costaría tres peticiones y un usuario, y lo que se prueba aquí no es la API.
    // LOS NIF SALEN DEL GENERADOR COMPARTIDO Y DE UNA BANDA PROPIA -del 301 en adelante-, y las
    // dos mitades las enseñó un rojo: el contenedor es UNO para todo el carril, `empresas.nif` es
    // único, y la primera versión de estos casos empezaba en el 1 y en el 100, que ya usaban el
    // contrato de Organización y media docena de fixturas más. En solitario salían verdes.
    private async Task<(Guid Empresa, Guid Serie)> UnaSerieNuevaAsync(string nif, TipoDeDocumento tipo)
    {
        DateTimeOffset ahora = TimeProvider.System.GetUtcNow();

        Empresa empresa = Empresa.Crear(
            Nif.De(nif),
            "Numeración " + nif,
            Direccion.De("Calle del Contador", "1", "28001", "Madrid", "Madrid", "ES"),
            "EUR",
            RegimenDeIva.General,
            ahora);

        Ejercicio ejercicio = Ejercicio.Crear(
            empresa.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), ahora);

        Serie serie = Serie.Crear(
            empresa.Id, ejercicio.Id, tipo, "SER", "{serie}-{numero:0000}", ahora);

        await using OrganizacionDbContext contexto = postgres.AbrirOrganizacion(empresa.Id);

        contexto.Empresas.Add(empresa);
        contexto.Ejercicios.Add(ejercicio);
        contexto.Series.Add(serie);
        await contexto.SaveChangesAsync();

        return (empresa.Id, serie.Id);
    }

    // El contador tal y como quedó EN LA BASE. Siempre desde un contexto nuevo: leerlo del que
    // acaba de escribir contestaría lo que el proceso cree, no lo que la transacción dejó.
    private async Task<long> ContadorDeAsync(Guid empresa, Guid serie)
    {
        await using OrganizacionDbContext contexto = postgres.AbrirOrganizacion(empresa);

        return (await contexto.Series.SingleAsync(fila => fila.Id == serie)).Contador;
    }
}
