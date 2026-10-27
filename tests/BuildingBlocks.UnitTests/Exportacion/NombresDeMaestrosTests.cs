using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Exportacion;

/// <summary>
/// Los nombres de una página se piden una vez, con los identificadores distintos y sin nulos, solo
/// con el permiso de ver el maestro y solo si hay alguno que pedir (ADR-0074 §3 y §5).
/// </summary>
public sealed class NombresDeMaestrosTests
{
    private static readonly Permiso s_ver = Permiso.De("organizacion.unidad-medida.ver");

    private static readonly Guid s_uno = Guid.CreateVersion7();

    private static readonly Guid s_dos = Guid.CreateVersion7();

    private readonly List<Guid[]> _preguntas = [];

    [Fact]
    public async Task Con_el_permiso_pregunta_una_vez_con_los_distintos_y_devuelve_lo_que_vuelve()
    {
        IReadOnlyDictionary<Guid, NombreDeMaestro> nombres = await NombresDeMaestros.PedirAsync(
            new Usuario(s_ver), s_ver, [s_uno, null, s_dos, s_uno], NombrarAsync, CancellationToken.None);

        _preguntas.ShouldHaveSingleItem().ShouldBe([s_uno, s_dos], ignoreOrder: true);
        nombres.Keys.ShouldBe([s_uno, s_dos], ignoreOrder: true);
        nombres[s_uno].ShouldBe(new NombreDeMaestro(s_uno.ToString("N"), null));
    }

    [Fact]
    public async Task Sin_el_permiso_no_pregunta_y_no_hay_nombres()
    {
        IReadOnlyDictionary<Guid, NombreDeMaestro> nombres = await NombresDeMaestros.PedirAsync(
            new Usuario(Permiso.De("organizacion.impuesto.ver")), s_ver, [s_uno], NombrarAsync, CancellationToken.None);

        _preguntas.ShouldBeEmpty();
        nombres.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sin_identificadores_no_pregunta_aunque_tenga_el_permiso()
    {
        IReadOnlyDictionary<Guid, NombreDeMaestro> nombres = await NombresDeMaestros.PedirAsync(
            new Usuario(s_ver), s_ver, [null, null], NombrarAsync, CancellationToken.None);

        _preguntas.ShouldBeEmpty();
        nombres.ShouldBeEmpty();
    }

    /// <summary>La que no mira el permiso, para el maestro que el listado ya exige ver.</summary>
    [Fact]
    public async Task Sin_permiso_que_mirar_pregunta_por_los_distintos_y_sin_identificadores_no()
    {
        IReadOnlyDictionary<Guid, NombreDeMaestro> nombres =
            await NombresDeMaestros.PedirAsync([s_dos, s_dos], NombrarAsync, CancellationToken.None);
        IReadOnlyDictionary<Guid, NombreDeMaestro> ninguno =
            await NombresDeMaestros.PedirAsync([null], NombrarAsync, CancellationToken.None);

        _preguntas.ShouldHaveSingleItem().ShouldBe([s_dos]);
        nombres.Keys.ShouldBe([s_dos]);
        ninguno.ShouldBeEmpty();
    }

    /// <summary>El puerto de mentira: nombra cada uno con su identificador y apunta.</summary>
    private Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> NombrarAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancelacion)
    {
        _preguntas.Add([.. ids]);

        return Task.FromResult<IReadOnlyDictionary<Guid, NombreDeMaestro>>(
            ids.ToDictionary(id => id, id => new NombreDeMaestro(id.ToString("N"), null)));
    }

    private sealed class Usuario(Permiso concedido) : IUsuarioActual
    {
        public bool EstaAutenticado => true;

        public Guid UsuarioId { get; } = Guid.CreateVersion7();

        public Guid EmpresaId { get; } = Guid.CreateVersion7();

        public bool Tiene(Permiso permiso) => permiso == concedido;
    }
}
