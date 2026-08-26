using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Identidad.Domain.Usuarios;
using Shouldly;

namespace Bastion.Identidad.UnitTests.Usuarios;

public sealed class UsuarioTests
{
    private static readonly DateTimeOffset s_ahora = new(2026, 8, 26, 9, 0, 0, TimeSpan.Zero);

    private static Usuario UnUsuario() =>
        Usuario.Crear(Correo.De("ana@ejemplo.es"), "Ana López", "hash-de-mentira", s_ahora);

    [Fact]
    public void Crear_NaceActivoYSinRastroDeAccesos()
    {
        Usuario usuario = UnUsuario();

        usuario.Estado.ShouldBe(EstadoDeUsuario.Activo);
        usuario.BloqueadoEn.ShouldBeNull();
        usuario.CreadoEn.ShouldBe(s_ahora);
        usuario.UltimoAccesoEn.ShouldBeNull();
        usuario.IntentosFallidos.ShouldBe(0);
        usuario.RechazadoHasta.ShouldBeNull();
        usuario.Membresias.ShouldBeEmpty();
    }

    [Fact]
    public void Crear_RecortaElNombre() =>
        Usuario.Crear(Correo.De("ana@ejemplo.es"), "  Ana López  ", "hash", s_ahora)
            .Nombre.ShouldBe("Ana López");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_SinNombre_Lanza(string nombre) =>
        Should.Throw<ArgumentException>(() =>
            Usuario.Crear(Correo.De("ana@ejemplo.es"), nombre, "hash", s_ahora));

    // La cuenta guarda el RESUMEN. Que el dominio no acepte una contraseña vacía no es cortesía:
    // un hash vacío que llegara a la columna haría que la comprobación de contraseña no tuviera
    // contra qué comparar.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_SinHash_Lanza(string hash) =>
        Should.Throw<ArgumentException>(() =>
            Usuario.Crear(Correo.De("ana@ejemplo.es"), "Ana", hash, s_ahora));

    // ---------------------------------------------------------------- R16: la baja lógica

    [Fact]
    public void Bloquear_DejaLaCuentaEnBajaLogicaConSuFecha()
    {
        Usuario usuario = UnUsuario();

        usuario.Bloquear(s_ahora);

        usuario.Estado.ShouldBe(EstadoDeUsuario.Bloqueado);
        usuario.BloqueadoEn.ShouldBe(s_ahora);
        usuario.PuedeIniciarSesion(s_ahora).ShouldBeFalse();
    }

    // La fecha de bloqueo es de la que se cuenta el plazo de conservación: volver a bloquear una
    // cuenta ya bloqueada no puede reiniciar ese contador.
    [Fact]
    public void Bloquear_DosVeces_ConservaLaPrimeraFecha()
    {
        Usuario usuario = UnUsuario();
        usuario.Bloquear(s_ahora);

        usuario.Bloquear(s_ahora.AddDays(30));

        usuario.BloqueadoEn.ShouldBe(s_ahora);
    }

    [Fact]
    public void Desbloquear_DevuelveLaCuentaAActivoYBorraLaFecha()
    {
        Usuario usuario = UnUsuario();
        usuario.Bloquear(s_ahora);

        usuario.Desbloquear();

        usuario.Estado.ShouldBe(EstadoDeUsuario.Activo);
        usuario.BloqueadoEn.ShouldBeNull();
        usuario.PuedeIniciarSesion(s_ahora).ShouldBeTrue();
    }

    // ------------------------------------------------- Bloqueo por intentos: el OTRO bloqueo

    [Fact]
    public void RegistrarIntentoFallido_PorDebajoDelTope_NoRechaza()
    {
        Usuario usuario = UnUsuario();

        for (int intento = 1; intento < Usuario.IntentosTolerados; intento++)
        {
            usuario.RegistrarIntentoFallido(s_ahora);
        }

        usuario.IntentosFallidos.ShouldBe(Usuario.IntentosTolerados - 1);
        usuario.EstaRechazado(s_ahora).ShouldBeFalse();
        usuario.PuedeIniciarSesion(s_ahora).ShouldBeTrue();
    }

    [Fact]
    public void RegistrarIntentoFallido_AlLlegarAlTope_RechazaDuranteLaEspera()
    {
        Usuario usuario = UnUsuario();

        for (int intento = 0; intento < Usuario.IntentosTolerados; intento++)
        {
            usuario.RegistrarIntentoFallido(s_ahora);
        }

        usuario.EstaRechazado(s_ahora).ShouldBeTrue();
        usuario.PuedeIniciarSesion(s_ahora).ShouldBeFalse();
        usuario.RechazadoHasta.ShouldBe(s_ahora + Usuario.EsperaTrasIntentosFallidos);
    }

    // El rechazo por intentos se levanta SOLO. Es la diferencia con el bloqueo de R16, y es la
    // razón de que sean dos campos: una fuerza bruta no puede dar de baja la cuenta de nadie.
    [Fact]
    public void ElRechazoPorIntentos_SeLevantaSolo_YNoTocaElEstadoDeLaCuenta()
    {
        Usuario usuario = UnUsuario();

        for (int intento = 0; intento < Usuario.IntentosTolerados; intento++)
        {
            usuario.RegistrarIntentoFallido(s_ahora);
        }

        DateTimeOffset despues = s_ahora + Usuario.EsperaTrasIntentosFallidos + TimeSpan.FromSeconds(1);

        usuario.EstaRechazado(despues).ShouldBeFalse();
        usuario.PuedeIniciarSesion(despues).ShouldBeTrue();
        usuario.Estado.ShouldBe(EstadoDeUsuario.Activo);
    }

    [Fact]
    public void RegistrarAccesoCorrecto_BorraElRastroDeIntentosYApuntaLaFecha()
    {
        Usuario usuario = UnUsuario();
        usuario.RegistrarIntentoFallido(s_ahora);
        usuario.RegistrarIntentoFallido(s_ahora);

        usuario.RegistrarAccesoCorrecto(s_ahora.AddMinutes(1));

        usuario.IntentosFallidos.ShouldBe(0);
        usuario.RechazadoHasta.ShouldBeNull();
        usuario.UltimoAccesoEn.ShouldBe(s_ahora.AddMinutes(1));
    }

    // Quien puede cambiar la contraseña ya ha demostrado que es el dueño: dejarlo rechazado
    // sería castigarle por el ataque que ha sufrido.
    [Fact]
    public void CambiarContrasena_LevantaElRechazoPorIntentos()
    {
        Usuario usuario = UnUsuario();

        for (int intento = 0; intento < Usuario.IntentosTolerados; intento++)
        {
            usuario.RegistrarIntentoFallido(s_ahora);
        }

        usuario.CambiarContrasena("hash-nuevo");

        usuario.HashDeContrasena.ShouldBe("hash-nuevo");
        usuario.IntentosFallidos.ShouldBe(0);
        usuario.EstaRechazado(s_ahora).ShouldBeFalse();
    }

    // Una cuenta dada de baja no vuelve sola aunque pase el tiempo: el bloqueo de R16 no caduca.
    [Fact]
    public void UnaCuentaBloqueada_NoInicaSesionNiDentroDeUnAno() =>
        Should.NotThrow(() =>
        {
            Usuario usuario = UnUsuario();
            usuario.Bloquear(s_ahora);
            usuario.PuedeIniciarSesion(s_ahora.AddYears(1)).ShouldBeFalse();
        });

    // ------------------------------------------------------------------ Pertenencia a empresas

    [Fact]
    public void Conceder_DaDeAltaLaPertenenciaSinRoles()
    {
        Usuario usuario = UnUsuario();
        var empresa = Guid.CreateVersion7();

        Membresia membresia = usuario.Conceder(empresa);

        usuario.PerteneceA(empresa).ShouldBeTrue();
        membresia.EmpresaId.ShouldBe(empresa);
        membresia.UsuarioId.ShouldBe(usuario.Id);
        membresia.Roles.ShouldBeEmpty();
    }

    // Pertenecer dos veces a la misma empresa no es pertenecer más: sería una segunda fila con
    // otros roles, y entonces «qué permisos tiene aquí» dejaría de tener una sola respuesta.
    [Fact]
    public void Conceder_DosVecesLaMismaEmpresa_DevuelveLaPertenenciaQueYaHabia()
    {
        Usuario usuario = UnUsuario();
        var empresa = Guid.CreateVersion7();

        Membresia primera = usuario.Conceder(empresa);
        Membresia segunda = usuario.Conceder(empresa);

        segunda.ShouldBeSameAs(primera);
        usuario.Membresias.Count.ShouldBe(1);
    }

    [Fact]
    public void Retirar_QuitaLaPertenenciaYSusRoles()
    {
        Usuario usuario = UnUsuario();
        var empresa = Guid.CreateVersion7();
        usuario.Conceder(empresa).AsignarRol(Guid.CreateVersion7());

        usuario.Retirar(empresa).ShouldBeTrue();

        usuario.PerteneceA(empresa).ShouldBeFalse();
        usuario.Membresias.ShouldBeEmpty();
    }

    [Fact]
    public void Retirar_DeUnaEmpresaALaQueNoPertenece_NoHaceNada() =>
        UnUsuario().Retirar(Guid.CreateVersion7()).ShouldBeFalse();

    // Los roles son POR EMPRESA. La misma persona contabiliza en una sociedad y solo consulta en
    // otra: si esto no se cumpliera, un permiso concedido para una empresa valdría en todas.
    [Fact]
    public void LosRoles_SonDeLaEmpresaEnLaQueSeConceden()
    {
        Usuario usuario = UnUsuario();
        var una = Guid.CreateVersion7();
        var otra = Guid.CreateVersion7();
        var contable = Guid.CreateVersion7();

        usuario.Conceder(una).AsignarRol(contable);
        usuario.Conceder(otra);

        usuario.EnEmpresa(una)!.Tiene(contable).ShouldBeTrue();
        usuario.EnEmpresa(otra)!.Tiene(contable).ShouldBeFalse();
    }

    [Fact]
    public void AsignarRol_DosVecesElMismo_NoLoDuplica()
    {
        Membresia membresia = UnUsuario().Conceder(Guid.CreateVersion7());
        var rol = Guid.CreateVersion7();

        membresia.AsignarRol(rol).ShouldBeTrue();
        membresia.AsignarRol(rol).ShouldBeFalse();
        membresia.Roles.Count.ShouldBe(1);
    }

    [Fact]
    public void RetirarRol_QuitaSoloEseRol()
    {
        Membresia membresia = UnUsuario().Conceder(Guid.CreateVersion7());
        var uno = Guid.CreateVersion7();
        var otro = Guid.CreateVersion7();
        membresia.AsignarRol(uno);
        membresia.AsignarRol(otro);

        membresia.RetirarRol(uno).ShouldBeTrue();

        membresia.Tiene(uno).ShouldBeFalse();
        membresia.Tiene(otro).ShouldBeTrue();
    }

    [Fact]
    public void RetirarRol_QueNoTenia_NoHaceNada() =>
        UnUsuario().Conceder(Guid.CreateVersion7()).RetirarRol(Guid.CreateVersion7()).ShouldBeFalse();
}
