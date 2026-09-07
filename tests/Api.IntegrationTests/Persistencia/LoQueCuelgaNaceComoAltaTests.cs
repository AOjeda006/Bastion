using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Terceros.Domain.Terceros;
using Bastion.Terceros.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Persistencia;

/// <summary>
/// Lo que se cuelga de una ficha ya guardada nace como <b>alta</b>, no como modificación.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin contenedor, y por eso NO lleva el rasgo <c>Integracion</c>.</b> Decidir el estado de una
/// entrada del seguidor de cambios necesita el proveedor y el modelo, no un servidor: es otra parte
/// del carril de integración que <i>no necesita el carril</i>, y por tanto tiene que poder
/// ejercerse con Docker parado. Es lo que convierte un fallo que solo se veía en la CI en un fallo
/// de dos segundos en la máquina de quien lo provoca.
/// </para>
/// <para>
/// <b>Qué defecto guarda.</b> EF Core decide si una entidad que aparece en una colección de un
/// padre ya seguido es un alta o una fila que ya existía <b>mirando si su clave viene puesta</b>.
/// El ejemplo de manual funciona porque allí la clave es un <c>identity</c> y vale cero hasta que
/// la base la genera. Aquí no: todas las claves de este sistema son <c>Guid</c> v7 que <b>pone el
/// dominio</b> en su fábrica, a propósito, por localidad de índice. Así que EF ve una clave puesta,
/// concluye «esto ya existe», marca la entrada como modificación y emite un <c>UPDATE</c> contra
/// una fila que no está. El <c>UPDATE</c> afecta a cero filas, EF lo interpreta como choque de
/// concurrencia y la política de errores lo traduce a un <b>412</b> — a una petición que era un
/// alta perfectamente correcta.
/// </para>
/// <para>
/// <b>Y no se ve.</b> No hay excepción al colgar, ni aviso al construir el modelo, ni nada que el
/// compilador pueda mirar: el estado se decide en tiempo de ejecución, dentro del seguidor. La
/// única señal es un 412 en una ruta donde nadie estaba compitiendo con nadie.
/// </para>
/// </remarks>
public sealed class LoQueCuelgaNaceComoAltaTests
{
    // No se conecta a nada, igual que en `LaTraduccionASqlTests`: construir el modelo y decidir el
    // estado de una entrada no abre conexión. El puerto 1 está ahí para que, si algún día alguien
    // escribe aquí algo que SÍ ejecute, falle en el acto en vez de esperar un tiempo de espera.
    private const string HaciaNingunSitio =
        "Host=127.0.0.1;Port=1;Database=nohay;Username=nadie;Password=nada;Timeout=1";

    // Inventado, y de los que no pueden existir: el control es el que sale de calcularlo sobre
    // esta cuenta de nueves, y la entidad `9999` no está asignada a nadie.
    private const string IbanInventado = "ES7899999999999999999999";

    private static readonly DateTimeOffset s_momento =
        new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Los tres hijos, colgados de una ficha que ya estaba guardada, salen <c>Added</c>.
    /// </summary>
    [Fact]
    public void Lo_que_se_cuelga_de_una_ficha_ya_guardada_sale_como_ALTA()
    {
        using TercerosDbContext contexto = Abrir();
        Tercero tercero = UnaFichaYaGuardada(contexto);

        Contacto contacto = tercero.AgregarContacto(
            "Nombre De Prueba", cargo: null, correo: null, telefono: null, s_momento);

        CuentaBancaria cuenta = tercero.AgregarCuentaBancaria(
            Iban.De(IbanInventado), bic: null, alias: null, esPreferente: true, s_momento);

        CondicionPago condicion = tercero.FijarCondicionPago(
            RolDeCondicionPago.Cliente, diasDePlazo: 30, diaDePagoFijo: null,
            descuentoPorProntoPago: null, s_momento);

        contexto.ChangeTracker.DetectChanges();

        Estado(contexto, contacto).ShouldBe(EntityState.Added, Porque(nameof(Contacto)));
        Estado(contexto, cuenta).ShouldBe(EntityState.Added, Porque(nameof(CuentaBancaria)));
        Estado(contexto, condicion).ShouldBe(EntityState.Added, Porque(nameof(CondicionPago)));
    }

    /// <summary>
    /// El arnés ve el modelo, ve la ficha como ya guardada y sabe producir una modificación.
    /// </summary>
    /// <remarks>
    /// Sin esto, el caso de arriba sale verde de las tres maneras en que puede estar roto: si el
    /// modelo no tiene las entidades —y entonces <c>Entry</c> no significa nada—, si la ficha no
    /// queda seguida —y entonces no hay ningún padre al que colgarle nada— y si
    /// <c>DetectChanges</c> no estuviera mirando las colecciones, en cuyo caso ningún estado
    /// cambiaría nunca y <c>Added</c> podría salir por no haberse tocado nada.
    /// </remarks>
    [Fact]
    public void El_arnes_ve_el_modelo_y_distingue_un_alta_de_una_modificacion()
    {
        using TercerosDbContext contexto = Abrir();

        foreach (Type hijo in new[] { typeof(Contacto), typeof(CuentaBancaria), typeof(CondicionPago) })
        {
            contexto.Model.FindEntityType(hijo).ShouldNotBeNull(
                $"{hijo.Name} no está en el modelo, así que `Entry` sobre uno de ellos no " +
                "afirmaría nada del mapeo real");
        }

        Tercero tercero = UnaFichaYaGuardada(contexto);

        contexto.Entry(tercero).State.ShouldBe(
            EntityState.Unchanged,
            "la ficha tiene que quedar seguida y sin cambios, que es como queda al leerla. Si " +
            "sale de otra manera, el caso de al lado no está colgando nada de un padre seguido");

        // Y que una modificación de verdad SÍ sale como modificación: es lo que distingue «este
        // arnés ve los cambios» de «este arnés no ve nada y todo se queda como estaba».
        tercero.Modificar(
            "Otra Razón Social",
            nombreComercial: null,
            Domicilio(),
            esCliente: true,
            esProveedor: false,
            RegimenFiscal.Comun());

        contexto.ChangeTracker.DetectChanges();

        contexto.Entry(tercero).State.ShouldBe(
            EntityState.Modified,
            "cambiar la razón social de una ficha seguida tiene que dejarla en `Modified`. Si no, " +
            "`DetectChanges` no está viendo nada y el `Added` del caso de al lado no significaría " +
            "que se ha decidido bien, sino que no se ha decidido");
    }

    private static string Porque(string hijo) =>
        $"{hijo} se ha colgado de una ficha que ya estaba guardada, así que es un ALTA. Si sale " +
        "`Modified`, EF va a emitir un UPDATE contra una fila que no existe: afectará a cero " +
        "filas, lo tomará por un choque de concurrencia y la política de errores lo traducirá a " +
        "un 412 sobre una petición que no competía con nadie. Pasa porque la clave la pone el " +
        "dominio —`Guid.CreateVersion7()` en la fábrica— y EF decide alta contra existente " +
        "mirando precisamente si la clave viene puesta.";

    private static EntityState Estado(TercerosDbContext contexto, object entidad) =>
        contexto.Entry(entidad).State;

    /// <summary>
    /// Una ficha en el estado en que la deja una lectura: seguida y sin cambios.
    /// </summary>
    /// <remarks>
    /// <c>Attach</c> y no una consulta, porque una consulta necesitaría servidor. Lo que se está
    /// ejerciendo es el camino de <c>NavigationFixer</c> —un hijo nuevo aparece en la colección de
    /// un padre que ya está en el mapa de identidad—, y ese camino es el mismo venga el padre de
    /// donde venga. Que el trayecto entero funcione contra PostgreSQL lo dice el carril de
    /// integración; que el estado se decida bien, lo dice esto, en dos segundos y sin Docker.
    /// </remarks>
    private static Tercero UnaFichaYaGuardada(TercerosDbContext contexto)
    {
        var tercero = Tercero.Crear(
            Guid.CreateVersion7(),
            IdentificacionFiscal.Espanola(Nif.De("B99999997")),
            "Razón Social",
            nombreComercial: null,
            Domicilio(),
            esCliente: true,
            esProveedor: false,
            RegimenFiscal.Comun(),
            s_momento);

        contexto.Attach(tercero);

        return tercero;
    }

    private static Direccion Domicilio() => Direccion.De(
        calle: "Calle de la Prueba",
        numero: "1",
        codigoPostal: "28001",
        poblacion: "Madrid",
        subdivision: "Madrid",
        pais: "ES");

    private static TercerosDbContext Abrir()
    {
        DbContextOptionsBuilder<TercerosDbContext> opciones = new();
        TercerosDbContext.Configurar(opciones, HaciaNingunSitio);

        return new TercerosDbContext(
            opciones.Options,
            new InquilinoDeMentira(),
            new SinAccesoALoBloqueado());
    }

    // Los mismos dobles que en `ElListadoDelArticulo32SeTraduceEnteroTests`: contestan en vez de
    // lanzar, porque aquí se construye el modelo con sus filtros de R8 y R16 puestos.
    private sealed class InquilinoDeMentira : IInquilinoActual
    {
        private static readonly Guid s_empresa = Guid.CreateVersion7();

        public Guid? EmpresaDelFiltro => s_empresa;

        public bool HayEmpresaActiva => true;

        public MotivoSinInquilino? MotivoDelAmbito => null;

        public IDisposable SinInquilino(MotivoSinInquilino motivo) => throw new NotSupportedException(
            "Aquí no se suspende el inquilinato: lo que se prueba es el estado de una entrada.");
    }

    private sealed class SinAccesoALoBloqueado : IAccesoALoBloqueado
    {
        public bool Abierto => false;

        public MotivoParaVerLoBloqueado? MotivoDelAmbito => null;

        public IDisposable ViendoLoBloqueado(MotivoParaVerLoBloqueado motivo) =>
            throw new NotSupportedException(
                "Aquí no se abre el ámbito de R16: lo que se prueba es el estado de una entrada.");
    }
}
