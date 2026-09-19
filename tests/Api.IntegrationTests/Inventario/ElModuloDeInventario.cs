using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Los tres casos de uso del ajuste con sus adaptadores REALES y los contextos que necesitan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Vive en su propio fichero desde el ítem 2.4</b>, cuando dejó de tener un solo cliente: lo
/// usan el caso del almacén bloqueado y el de la serie. Se cablea a mano y no se pide al
/// contenedor del host porque el host todavía no tiene ningún borde de Inventario —los endpoints
/// son del 2.4 y del 2.5—, así que no hay petición que resolver. Que el cableado de verdad
/// registre estas mismas piezas se comprueba en otro sitio:
/// <c>AgregarCasosDeUsoDeInventario</c> tiene su propio caso en el carril rápido.
/// </para>
/// <para>
/// <b>Los contextos se abren a mano y no con <c>postgres.Abrir…</c></b>: el doble de aquellos
/// lleva <c>AccesoCerrado</c>, que lanza en cuanto alguien abre un ámbito del art. 32, y los
/// puertos de almacén y de ubicación abren uno. Es el mismo motivo por el que
/// <c>UnaEstanteriaBloqueadaSigueExistiendoTests</c> abre el suyo.
/// </para>
/// <para>
/// <b>El acceso es uno solo y compartido por los dos puertos</b>, como en el contenedor de la
/// API: el ámbito es <c>AsyncLocal</c> y se abre y se cierra dentro de cada llamada, así que
/// compartirlo no deja ninguna puerta abierta entre una y la siguiente.
/// </para>
/// </remarks>
internal sealed class ElModuloDeInventario : IAsyncDisposable
{
    private readonly OrganizacionDbContext _organizacion;
    private readonly CatalogoDbContext _catalogo;
    private readonly InventarioDbContext _inventario;

    internal ElModuloDeInventario(PostgresConTodosLosModulos postgres, Guid empresaId)
    {
        AccesoALoBloqueado acceso =
            new(NullLogger<AccesoALoBloqueado>.Instance, new NadieEnConcreto());

        DbContextOptionsBuilder<OrganizacionDbContext> deOrganizacion = new();
        OrganizacionDbContext.Configurar(deOrganizacion, postgres.CadenaDeConexion);
        _organizacion = new OrganizacionDbContext(
            deOrganizacion.Options, new InquilinoFijo(empresaId), acceso);

        DbContextOptionsBuilder<CatalogoDbContext> deCatalogo = new();
        CatalogoDbContext.Configurar(deCatalogo, postgres.CadenaDeConexion);
        _catalogo = new CatalogoDbContext(
            deCatalogo.Options, new InquilinoFijo(empresaId), acceso);

        _inventario = postgres.AbrirInventario(empresaId);

        RepositorioDeAjustes ajustes = new(_inventario);
        UnidadDeTrabajoDeInventario unidadDeTrabajo = new(_inventario);
        ConsultaDeAlmacenes almacenes = new(_organizacion, acceso);

        Alta = new AbrirAjuste(
            new ElUsuarioDeLaEmpresa(empresaId),
            ajustes,
            new ConsultaDeEmpresas(_organizacion),
            almacenes,
            new ConsultaDeSeries(_organizacion),
            new ConsultaDeUbicaciones(_organizacion, acceso),
            new ConsultaDeArticulos(_catalogo),
            new ConsultaDeUnidadesDeMedida(_organizacion),
            unidadDeTrabajo,
            TimeProvider.System);

        Confirmacion = new ConfirmarAjuste(
            ajustes,
            new NumeradorDeSeriesDeInventario(_inventario, new InquilinoFijo(empresaId)),
            unidadDeTrabajo,
            TimeProvider.System);

        Lectura = new MovimientosDelDocumento(ajustes, almacenes);
    }

    internal AbrirAjuste Alta { get; }

    internal ConfirmarAjuste Confirmacion { get; }

    internal MovimientosDelDocumento Lectura { get; }

    /// <summary>Confirma un ajuste con una transacción abierta, como llegaría de verdad.</summary>
    /// <remarks>
    /// <b>La transacción no es adorno del caso: el mecanismo de numeración revienta sin
    /// ella</b>, y a propósito —un número tomado fuera queda gastado si el documento no llega
    /// a guardarse—. En una petición de verdad la abre el filtro de idempotencia, que este
    /// cableado a mano no tiene porque no hay borde todavía; aquí la abre esto, con el mismo
    /// criterio: solo se confirma lo que sale bien.
    /// </remarks>
    /// <param name="ajusteId">El documento que confirmar.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal async Task<Resultado<AjusteDto>> ConfirmarAsync(Guid ajusteId)
    {
        await using IDbContextTransaction transaccion =
            await _inventario.Database.BeginTransactionAsync();

        Resultado<AjusteDto> confirmacion =
            await Confirmacion.EjecutarAsync(ajusteId, CancellationToken.None);

        if (confirmacion.EsCorrecto)
        {
            await transaccion.CommitAsync();
        }
        else
        {
            await transaccion.RollbackAsync();
        }

        return confirmacion;
    }

    public async ValueTask DisposeAsync()
    {
        await _inventario.DisposeAsync();
        await _catalogo.DisposeAsync();
        await _organizacion.DisposeAsync();
    }
}

/// <summary>De dónde saca el alta la empresa (R8): del usuario, nunca de la petición.</summary>
/// <remarks>
/// No concede ningún permiso —quien decide si la operación se permite es la autorización de la
/// API, y esta clase no la sustituye—: lo único que aporta es la empresa, que es justo lo que
/// la R8 dice que no puede viajar en el cuerpo.
/// </remarks>
/// <param name="empresaId">La empresa activa.</param>
internal sealed class ElUsuarioDeLaEmpresa(Guid empresaId) : IUsuarioActual
{
    public bool EstaAutenticado => true;

    public Guid UsuarioId => throw new NotSupportedException(
        "El alta de un ajuste no firma la fila: de eso se encarga el interceptor de auditoría.");

    public Guid EmpresaId => empresaId;

    public bool Tiene(Permiso permiso) => false;
}

/// <summary>
/// Quien queda anotado al abrir un ámbito del art. 32 cuando no hay nadie identificado.
/// </summary>
/// <remarks>
/// El <c>AccesoALoBloqueado</c> de verdad anota en el registro quién pidió la apertura; aquí
/// no hay petición HTTP, así que no hay nadie. Lanzar en vez de inventarse un identificador es
/// lo que hace imposible una traza con un usuario falso.
/// </remarks>
internal sealed class NadieEnConcreto : IUsuarioActual
{
    public bool EstaAutenticado => false;

    public Guid UsuarioId => throw new NotSupportedException("No hay nadie autenticado.");

    public Guid EmpresaId => throw new NotSupportedException("No hay nadie autenticado.");

    public bool Tiene(Permiso permiso) => false;
}
