using System.Globalization;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Divisas;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Domain.Impuestos;
using Bastion.Organizacion.Domain.Ubicaciones;
using Bastion.Organizacion.Domain.Unidades;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// <c>NombresDeAsync</c> de los cinco puertos de Organización contra PostgreSQL: qué maestros
/// nombra y cuáles no (ADR-0074).
/// </summary>
/// <remarks>
/// <para>
/// <b>La regla es la de la ficha de cada maestro</b>, la de su <c>GET …/{id}</c>, que es lo que la
/// pantalla enseña: lo de otra empresa no vuelve, lo bloqueado tampoco, y lo retirado sí. Lo que
/// decide cada respuesta son los filtros del contexto, y eso solo se ve preguntándole a la base.
/// </para>
/// <para>
/// <b>Cada ausencia tiene su contraria</b>, como en <c>LosPuertosDeLecturaTests</c>. El almacén y la
/// ubicación bloqueados se nombran antes de bloquearlos, así que lo que los saca es el bloqueo y
/// no otra cosa; y el de la otra empresa se nombra en la suya. Las afirmaciones comparan el
/// diccionario entero: un puerto que devolviera todo, o nada, no pasa ninguna.
/// </para>
/// <para>
/// <b>Semillas: las empresas, de la 640 a la 643.</b> Del 600 al 607 son de
/// <c>UnaEstanteriaBloqueadaSigueExistiendoTests</c> y del 620 al 625 de
/// <c>ElPuertoDeSeriesTests</c>; del 640 al 649 no había ninguna, ni por literal ni por cálculo.
/// Las unidades, los tramos y las divisas llevan su código, que no choca con los de
/// <c>LosPuertosDeLecturaTests</c> ni con los de <c>LaConsultaDeConversionesTests</c>.
/// </para>
/// </remarks>
[Trait("Category", "Integracion")]
[Collection(ColeccionDePostgres.Nombre)]
public sealed class LosNombresPorLotesTests(PostgresDeVerdad postgres)
{
    private const string LetrasDeControl = "TRWAGMYFPDXBNJZSQVHLCKE";

    private static readonly DateTimeOffset s_momento = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Un almacén se nombra con su código y su nombre; el bloqueado, el de otra empresa y el
    /// inventado no vuelven.
    /// </summary>
    /// <remarks>
    /// <b>El bloqueado es la casilla que separa este método de <c>EstadoDeAsync</c></b>, que sí lo
    /// ve, porque abre el ámbito del art. 32 para resolver lo viejo. Un nombre es para enseñarlo, y
    /// la ficha de un almacén bloqueado contesta <c>404</c>: si el puerto abriera el ámbito, la
    /// exportación enseñaría lo que la pantalla no.
    /// </remarks>
    [Fact]
    public async Task El_almacen_se_nombra_y_ni_el_bloqueado_ni_el_ajeno_ni_el_inventado_vuelven()
    {
        Guid propia = await EmpresaAsync(640);
        Guid ajena = await EmpresaAsync(641);

        Almacen norte = await AlmacenAsync(propia, "NAVE-N", "Nave norte");
        Almacen sur = await AlmacenAsync(propia, "NAVE-S", "Nave sur");
        Almacen cerrado = await AlmacenAsync(propia, "NAVE-C", "Nave cerrada");
        Almacen deLaOtra = await AlmacenAsync(ajena, "NAVE-O", "Nave de la otra");
        var inventado = Guid.CreateVersion7();

        (await NombresDeAlmacenesAsync(propia, [cerrado.Id]))
            .ShouldBe(
                new Dictionary<Guid, NombreDeMaestro> { [cerrado.Id] = new("NAVE-C", "Nave cerrada") },
                ignoreOrder: true,
                "antes del bloqueo se nombra: lo que lo saca después es el bloqueo");

        await GuardarAsync(propia, contexto => contexto.Almacenes
            .Single(fila => fila.Id == cerrado.Id)
            .Bloquear(MotivoDeBloqueo.CeseDeUso, s_momento));

        (await NombresDeAlmacenesAsync(propia, [norte.Id, sur.Id, cerrado.Id, deLaOtra.Id, inventado]))
            .ShouldBe(
                new Dictionary<Guid, NombreDeMaestro>
                {
                    [norte.Id] = new("NAVE-N", "Nave norte"),
                    [sur.Id] = new("NAVE-S", "Nave sur"),
                },
                ignoreOrder: true,
                "los dos de la empresa con su nombre, y ni el bloqueado, ni el de otra empresa, ni " +
                "el inventado");

        (await NombresDeAlmacenesAsync(ajena, [deLaOtra.Id]))
            .ShouldBe(
                new Dictionary<Guid, NombreDeMaestro> { [deLaOtra.Id] = new("NAVE-O", "Nave de la otra") },
                ignoreOrder: true,
                "en su propia empresa sí vuelve");
    }

    /// <summary>
    /// Una ubicación se nombra con su código y su descripción, o sin ella; la bloqueada no vuelve,
    /// y la de un almacén bloqueado sí.
    /// </summary>
    /// <remarks>
    /// <b>La de un almacén bloqueado vuelve porque su ficha la enseña</b>: el filtro de R16 de las
    /// ubicaciones mira su propio bloqueo, y <c>GET …/ubicaciones/{id}</c> no une el almacén. Es lo
    /// contrario de lo que contesta <c>EstadoDeAsync</c>, que hereda el peor de los dos, y lo es a
    /// propósito: aquel decide si un movimiento puede apuntar a ese hueco, y este solo lo nombra.
    /// </remarks>
    [Fact]
    public async Task La_ubicacion_se_nombra_por_su_propio_bloqueo_y_no_por_el_de_su_almacen()
    {
        Guid propia = await EmpresaAsync(642);
        Guid ajena = await EmpresaAsync(643);

        Almacen abierto = await AlmacenAsync(propia, "NAVE-A", "Nave abierta");
        Almacen cerrado = await AlmacenAsync(propia, "NAVE-B", "Nave que se cierra");
        Almacen deLaOtra = await AlmacenAsync(ajena, "NAVE-X", "Nave de la otra");

        Ubicacion conDescripcion = await UbicacionAsync(propia, abierto.Id, "A-01-1", "Junto al muelle");
        Ubicacion sinDescripcion = await UbicacionAsync(propia, abierto.Id, "A-01-2", null);
        Ubicacion bloqueada = await UbicacionAsync(propia, abierto.Id, "A-01-3", "La que se bloquea");
        Ubicacion enElCerrado = await UbicacionAsync(propia, cerrado.Id, "B-01-1", "En la nave cerrada");
        Ubicacion ajenaUbicacion = await UbicacionAsync(ajena, deLaOtra.Id, "X-01-1", "De la otra");
        var inventada = Guid.CreateVersion7();

        (await NombresDeUbicacionesAsync(propia, [bloqueada.Id]))
            .ShouldBe(
                new Dictionary<Guid, NombreDeMaestro> { [bloqueada.Id] = new("A-01-3", "La que se bloquea") },
                ignoreOrder: true,
                "antes del bloqueo se nombra: lo que la saca después es el bloqueo");

        await GuardarAsync(propia, contexto => contexto.Ubicaciones
            .Single(fila => fila.Id == bloqueada.Id)
            .Bloquear(MotivoDeBloqueo.CeseDeUso, s_momento));

        await GuardarAsync(propia, contexto => contexto.Almacenes
            .Single(fila => fila.Id == cerrado.Id)
            .Bloquear(MotivoDeBloqueo.CeseDeUso, s_momento));

        (await NombresDeUbicacionesAsync(
            propia,
            [conDescripcion.Id, sinDescripcion.Id, bloqueada.Id, enElCerrado.Id, ajenaUbicacion.Id, inventada]))
            .ShouldBe(
                new Dictionary<Guid, NombreDeMaestro>
                {
                    [conDescripcion.Id] = new("A-01-1", "Junto al muelle"),
                    [sinDescripcion.Id] = new("A-01-2", null),
                    [enElCerrado.Id] = new("B-01-1", "En la nave cerrada"),
                },
                ignoreOrder: true,
                "las de la empresa, también la de la nave cerrada, y ni la bloqueada, ni la de otra " +
                "empresa, ni la inventada");

        (await NombresDeUbicacionesAsync(ajena, [ajenaUbicacion.Id]))
            .ShouldBe(
                new Dictionary<Guid, NombreDeMaestro> { [ajenaUbicacion.Id] = new("X-01-1", "De la otra") },
                ignoreOrder: true,
                "en su propia empresa sí vuelve");
    }

    /// <summary>
    /// Las unidades, los tramos y las divisas se nombran sin empresa, y lo retirado y lo que ya no
    /// rige, también.
    /// </summary>
    /// <remarks>
    /// <b>Por el contexto de <see cref="PostgresDeVerdad.AbrirContexto"/></b>, cuyo inquilino y cuyo
    /// acceso lanzan en cuanto alguien les pregunta, como en <c>LosPuertosDeLecturaTests</c>: que
    /// los tres contesten es la prueba de que son maestros de instalación, sin filtro de empresa ni
    /// de bloqueo. Lo retirado vuelve porque la retirada no es un filtro: su ficha lo enseña, y
    /// sigue resolviendo lo viejo.
    /// </remarks>
    [Fact]
    public async Task Las_unidades_los_tramos_y_las_divisas_se_nombran_sin_empresa_y_lo_retirado_tambien()
    {
        var caja = UnidadMedida.Crear("PNC", "Caja del nombre", 0, s_momento);
        var retirada = UnidadMedida.Crear("PNR", "Unidad del nombre retirada", 0, s_momento);
        Impuesto vigente = Tramo("PNO-VIG", "Tramo que rige", new DateOnly(2012, 9, 1), null);
        Impuesto cerrado = Tramo("PNO-CERR", "Tramo que ya no rige", new DateOnly(2010, 7, 1), new DateOnly(2012, 8, 31));
        var libra = Divisa.Crear("GBP", "Libra esterlina", s_momento);
        var franco = Divisa.Crear("CHF", "Franco suizo", s_momento);
        var inventado = Guid.CreateVersion7();

        retirada.Retirar();
        franco.Retirar();

        await using (OrganizacionDbContext alta = postgres.AbrirContexto())
        {
            alta.UnidadesDeMedida.AddRange(caja, retirada);
            alta.Impuestos.AddRange(vigente, cerrado);
            alta.Divisas.AddRange(libra, franco);

            await alta.SaveChangesAsync(CancellationToken.None);
        }

        await using OrganizacionDbContext contexto = postgres.AbrirContexto();

        (await new ConsultaDeUnidadesDeMedida(contexto)
            .NombresDeAsync([caja.Id, retirada.Id, inventado], CancellationToken.None))
            .ShouldBe(
                new Dictionary<Guid, NombreDeMaestro>
                {
                    [caja.Id] = new("PNC", "Caja del nombre"),
                    [retirada.Id] = new("PNR", "Unidad del nombre retirada"),
                },
                ignoreOrder: true,
                "las dos unidades, también la retirada, y no la inventada");

        (await new ConsultaDeImpuestos(contexto)
            .NombresDeAsync([vigente.Id, cerrado.Id, inventado], CancellationToken.None))
            .ShouldBe(
                new Dictionary<Guid, NombreDeMaestro>
                {
                    [vigente.Id] = new("PNO-VIG", "Tramo que rige"),
                    [cerrado.Id] = new("PNO-CERR", "Tramo que ya no rige"),
                },
                ignoreOrder: true,
                "los dos tramos, rijan o no, y no el inventado");

        (await new ConsultaDeDivisas(contexto)
            .NombresDeAsync([libra.Id, franco.Id, inventado], CancellationToken.None))
            .ShouldBe(
                new Dictionary<Guid, NombreDeMaestro>
                {
                    [libra.Id] = new("GBP", "Libra esterlina"),
                    [franco.Id] = new("CHF", "Franco suizo"),
                },
                ignoreOrder: true,
                "las dos divisas, también la retirada, y no la inventada");
    }

    private static Impuesto Tramo(string codigo, string nombre, DateOnly desde, DateOnly? hasta) =>
        Impuesto.Crear(codigo, nombre, TipoDeImpuesto.Iva, 21.00m, desde, hasta, null, null, s_momento);

    // El tipo concreto y no la interfaz, como en `LosPuertosDeLecturaTests`: CA1859 está como error
    // en este repositorio, y que la implementación case con su contrato lo comprueba el compilador
    // en `ModuloDeOrganizacion`, que la registra bajo él.
    private async Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> NombresDeAlmacenesAsync(
        Guid empresaId, IReadOnlyCollection<Guid> almacenes)
    {
        AccesoALoBloqueado acceso = Acceso();

        await using OrganizacionDbContext contexto = AbrirContexto(empresaId, acceso);

        return await new ConsultaDeAlmacenes(contexto, acceso)
            .NombresDeAsync(almacenes, CancellationToken.None);
    }

    private async Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> NombresDeUbicacionesAsync(
        Guid empresaId, IReadOnlyCollection<Guid> ubicaciones)
    {
        AccesoALoBloqueado acceso = Acceso();

        await using OrganizacionDbContext contexto = AbrirContexto(empresaId, acceso);

        return await new ConsultaDeUbicaciones(contexto, acceso)
            .NombresDeAsync(ubicaciones, CancellationToken.None);
    }

    private async Task<Guid> EmpresaAsync(int semilla)
    {
        var empresa = Empresa.Crear(
            Nif.De(NifInventado(semilla)),
            string.Create(CultureInfo.InvariantCulture, $"Empresa de prueba {semilla}"),
            Domicilio(),
            "EUR",
            RegimenDeIva.General,
            s_momento);

        await GuardarAsync(empresa.Id, contexto => contexto.Empresas.Add(empresa));

        return empresa.Id;
    }

    private async Task<Almacen> AlmacenAsync(Guid empresaId, string codigo, string nombre)
    {
        var almacen = Almacen.Crear(empresaId, codigo, nombre, Domicilio(), TipoDeAlmacen.Fisico, s_momento);

        await GuardarAsync(empresaId, contexto => contexto.Almacenes.Add(almacen));

        return almacen;
    }

    private async Task<Ubicacion> UbicacionAsync(
        Guid empresaId, Guid almacenId, string codigo, string? descripcion)
    {
        var ubicacion = Ubicacion.Crear(empresaId, almacenId, codigo, null, null, null, descripcion, s_momento);

        await GuardarAsync(empresaId, contexto => contexto.Ubicaciones.Add(ubicacion));

        return ubicacion;
    }

    private async Task GuardarAsync(Guid empresaId, Action<OrganizacionDbContext> cambio)
    {
        await using OrganizacionDbContext contexto = AbrirContexto(empresaId, Acceso());

        cambio(contexto);

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    // El contexto se abre a mano y no con `postgres.AbrirContexto()`, como en
    // `UnaEstanteriaBloqueadaSigueExistiendoTests`: el doble de aquel lanza en cuanto alguien le
    // pregunta por la empresa, y los almacenes y las ubicaciones se la preguntan en cada consulta.
    private OrganizacionDbContext AbrirContexto(Guid empresaId, IAccesoALoBloqueado acceso)
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, postgres.CadenaDeConexion);

        return new OrganizacionDbContext(opciones.Options, new InquilinoDeLaEmpresa(empresaId), acceso);
    }

    private static AccesoALoBloqueado Acceso() =>
        new(NullLogger<AccesoALoBloqueado>.Instance, new NadieEnConcreto());

    private static Direccion Domicilio() =>
        Direccion.De("Gran Vía", "31", "28013", "Madrid", "Madrid", "ES");

    private static string NifInventado(int numero) =>
        numero.ToString("D8", CultureInfo.InvariantCulture) + LetrasDeControl[numero % 23];

    /// <summary>Un inquilino fijo, que es lo que hay dentro de una petición ordinaria.</summary>
    /// <param name="empresaId">La empresa activa.</param>
    private sealed class InquilinoDeLaEmpresa(Guid empresaId) : IInquilinoActual
    {
        public bool HayEmpresaActiva => true;

        public Guid? EmpresaDelFiltro => empresaId;

        public MotivoSinInquilino? MotivoDelAmbito => null;

        public IDisposable SinInquilino(MotivoSinInquilino motivo) => throw new NotSupportedException(
            "Estos casos no suspenden el inquilinato: lo que comprueban es que está puesto.");
    }

    /// <summary>Nadie autenticado, que es lo que el acceso de verdad espera fuera de una petición.</summary>
    private sealed class NadieEnConcreto : IUsuarioActual
    {
        public bool EstaAutenticado => false;

        public Guid UsuarioId => throw new NotSupportedException("No hay nadie autenticado.");

        public Guid EmpresaId => throw new NotSupportedException("No hay nadie autenticado.");

        public bool Tiene(Permiso permiso) => false;
    }
}
