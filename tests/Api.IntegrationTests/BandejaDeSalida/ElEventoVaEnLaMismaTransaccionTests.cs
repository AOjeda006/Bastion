using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// La primera de las tres cláusulas del R12: que el evento y la escritura de negocio caigan en la
/// <b>misma</b> transacción.
/// </summary>
/// <remarks>
/// <para>
/// Un evento escrito fuera de la transacción del cambio miente en las dos direcciones, igual que
/// una traza: un alta confirmada cuyo evento se perdió —y el módulo que tenía que reaccionar no
/// reacciona nunca— y un evento de un alta que se revirtió —y el módulo reacciona a algo que no
/// ha pasado—. Las dos se descubren semanas después, cuando ya nadie relaciona el descuadre con
/// el alta de aquel día.
/// </para>
/// <para>
/// <b>Aquí no corre el publicador.</b> Esta clase no levanta el host a propósito: el publicador
/// marca las filas que despacha, y un <c>UPDATE</c> le cambia el <c>xmin</c> a la fila. La
/// comparación que prueba la atomicidad exige una cola que nadie esté vaciando por detrás.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido, con las migraciones aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElEventoVaEnLaMismaTransaccionTests(PostgresConTodosLosModulos postgres)
{
    [Fact]
    public async Task La_empresa_y_su_evento_los_escribe_LA_MISMA_transaccion()
    {
        // LA PRUEBA QUE NO SE PUEDE FINGIR. Los otros dos tests de este fichero los aprueba
        // también la ruta de volcar el evento DESPUÉS de que `SaveChanges` haya ido bien: con esa
        // ruta, el guardado que revienta tampoco deja evento —no se llega a volcar— y el que va
        // bien deja las dos cosas. Lo que esa ruta no puede es compartir transacción.
        //
        // PostgreSQL numera cada transacción y guarda en cada fila la que la insertó, en la
        // columna de sistema `xmin`. Dos filas con el mismo `xmin` entraron juntas; dos escrituras
        // seguidas no pueden compartirlo. Y no hay nada que instrumentar: lo cuenta la base.
        (Empresa empresa, EmpresaCreada evento) = Nueva("00000048W", "Atómica, S.L.");

        await using (OrganizacionDbContext contexto = postgres.AbrirOrganizacionConBandeja(empresa.Id))
        {
            contexto.Empresas.Add(empresa);
            await contexto.SaveChangesAsync();
        }

        string deLaFila = await TransaccionAsync("organizacion.empresas", $"id = {Comillas(empresa.Id)}");
        string deLaCola = await TransaccionAsync(
            $"{ConfiguracionDeLaBandeja.Esquema}.{ConfiguracionDeLaBandeja.Tabla}",
            $"evento_id = {Comillas(evento.EventoId)}");

        deLaCola.ShouldBe(
            deLaFila,
            "el alta y su evento los ha escrito una transacción distinta cada uno: hay un momento " +
            "en el que uno de los dos está confirmado y el otro no");
    }

    [Fact]
    public async Task Un_guardado_que_revienta_no_deja_ni_la_empresa_ni_su_evento()
    {
        // El choque se provoca aquí y no por la API porque el caso de uso comprueba el NIF antes y
        // devuelve un 409 sin llegar a guardar, que es justo lo que tiene que hacer. Lo que hace
        // falta es un guardado que reviente DENTRO de `SaveChanges`, y eso lo da el índice único
        // del NIF con las dos empresas en el mismo lote.
        (Empresa ocupante, EmpresaCreada _) = Nueva("00000049A", "La que ocupa el NIF");

        await using (OrganizacionDbContext previo = postgres.AbrirOrganizacionConBandeja(ocupante.Id))
        {
            previo.Empresas.Add(ocupante);
            await previo.SaveChangesAsync();
        }

        (Empresa buena, EmpresaCreada suyo) = Nueva("00000050G", "La que iba a entrar");
        (Empresa chocante, EmpresaCreada _) = Nueva("00000049A", "La que choca");

        await using (OrganizacionDbContext contexto = postgres.AbrirOrganizacionConBandeja(buena.Id))
        {
            contexto.Empresas.AddRange(buena, chocante);

            await Should.ThrowAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
        }

        // Si el evento se volcara por su cuenta —en otro contexto, en otra transacción, o «al
        // mejor esfuerzo» después del guardado—, aquí quedaría el evento de un alta que nunca
        // ocurrió, y el módulo que reaccionara a él trabajaría sobre una empresa que no existe.
        (await EnLaColaAsync(suyo.EventoId)).ShouldBeNull(
            "el evento de un alta que se revirtió no puede sobrevivir al alta");

        await using OrganizacionDbContext lectura = postgres.AbrirOrganizacion(buena.Id);

        (await lectura.Empresas.AnyAsync(fila => fila.Id == buena.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Y_uno_que_va_bien_deja_el_evento_entero_y_pendiente()
    {
        // El control positivo de los dos anteriores. Sin él, un interceptor que no volcara NADA
        // los pasaría los dos con nota.
        (Empresa empresa, EmpresaCreada evento) = Nueva("00000051M", "La que sí entra");

        await using (OrganizacionDbContext contexto = postgres.AbrirOrganizacionConBandeja(empresa.Id))
        {
            contexto.Empresas.Add(empresa);
            await contexto.SaveChangesAsync();
        }

        EventoDeLaBandeja fila = (await EnLaColaAsync(evento.EventoId)).ShouldNotBeNull();

        fila.Nombre.ShouldBe(EmpresaCreada.Nombre);
        fila.Estado.ShouldBe(EstadoDelEnvio.Pendiente);
        fila.Intentos.ShouldBe(0);
        fila.PublicadoEn.ShouldBeNull();

        // La empresa activa cuando se escribió, que es de quién es la fila. NO es la empresa de la
        // que habla el evento —eso va dentro del cuerpo—: es la que estaba operando.
        fila.EmpresaId.ShouldBe(empresa.Id);
        fila.SinInquilino.ShouldBeNull();

        // El cuerpo lleva el evento entero, con su identificador: es lo que hace que reprocesar se
        // pueda distinguir de procesar por primera vez.
        fila.Cuerpo.ShouldContain(evento.EventoId.ToString());
        fila.Cuerpo.ShouldContain(empresa.Nif.Valor);
    }

    [Fact]
    public async Task Guardar_dos_veces_el_mismo_agregado_no_encola_el_hecho_dos_veces()
    {
        // El agregado lleva sus eventos en la mano hasta que se guarda, y el interceptor se los
        // quita CUANDO el guardado ha ido bien. Sin eso, el segundo `SaveChanges` del mismo
        // contexto volvería a volcar el mismo hecho, y el índice único de `evento_id` lo
        // convertiría en un error de guardado en una operación que no tenía nada que ver.
        (Empresa empresa, EmpresaCreada evento) = Nueva("00000052Y", "La que se guarda dos veces");

        await using OrganizacionDbContext contexto = postgres.AbrirOrganizacionConBandeja(empresa.Id);

        contexto.Empresas.Add(empresa);
        await contexto.SaveChangesAsync();

        empresa.Modificar(
            "La que se guarda dos veces, S.L.", Fiscal(), empresa.DivisaBase, empresa.RegimenDeIva);

        await Should.NotThrowAsync(() => contexto.SaveChangesAsync());

        await using ContextoDeLaBandeja cola = postgres.AbrirBandejaEntera();

        (await cola.Bandeja.CountAsync(fila => fila.EventoId == evento.EventoId)).ShouldBe(
            1,
            "el hecho ocurrió una vez, así que en la cola tiene que estar una vez");
    }

    private async Task<EventoDeLaBandeja?> EnLaColaAsync(Guid eventoId)
    {
        await using ContextoDeLaBandeja cola = postgres.AbrirBandejaEntera();

        return await cola.Bandeja.SingleOrDefaultAsync(fila => fila.EventoId == eventoId);
    }

    private async Task<string> TransaccionAsync(string tabla, string filtro)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new($"SELECT xmin::text FROM {tabla} WHERE {filtro}", conexion);

        return (string)(await orden.ExecuteScalarAsync())!;
    }

    // Un identificador entrecomillado para SQL. Los valores son `Guid` recién creados aquí, no
    // entrada de nadie, así que esto es forma y no saneamiento.
    private static string Comillas(Guid valor) => $"'{valor}'";

    // El caso de uso hace exactamente esto: crea el agregado y le registra el hecho antes de
    // pasárselo al repositorio. Se repite aquí porque la API no puede provocar un guardado que
    // reviente a mitad, que es lo que hace falta para el segundo test.
    private static (Empresa Empresa, EmpresaCreada Evento) Nueva(string nif, string razonSocial)
    {
        var empresa = Empresa.Crear(Nif.De(nif), razonSocial, Fiscal(), "EUR", RegimenDeIva.General,
            TimeProvider.System.GetUtcNow());

        EmpresaCreada evento = new(empresa.Id, empresa.Nif.Valor, empresa.RazonSocial, empresa.DivisaBase);

        empresa.Registrar(evento);

        return (empresa, evento);
    }

    private static Direccion Fiscal() =>
        Direccion.De("Calle de la Bandeja", "8", "28001", "Madrid", "Madrid", "ES");
}
