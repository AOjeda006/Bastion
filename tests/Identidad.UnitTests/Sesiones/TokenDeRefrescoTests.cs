using Bastion.Identidad.Domain.Sesiones;
using Shouldly;

namespace Bastion.Identidad.UnitTests.Sesiones;

public sealed class TokenDeRefrescoTests
{
    private static readonly DateTimeOffset s_ahora = new(2026, 8, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_duracion = TimeSpan.FromDays(14);

    private static TokenDeRefresco Emitido(Guid? familia = null) =>
        TokenDeRefresco.Emitir(
            Guid.CreateVersion7(),
            familia ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "resumen-de-mentira",
            s_ahora,
            s_duracion);

    [Fact]
    public void Emitir_NaceVigenteYSinCanjearNiRevocar()
    {
        TokenDeRefresco token = Emitido();

        token.EstaVigente(s_ahora).ShouldBeTrue();
        token.EstaCanjeado.ShouldBeFalse();
        token.CanjeadoEn.ShouldBeNull();
        token.RevocadoEn.ShouldBeNull();
        token.SustituidoPorId.ShouldBeNull();
        token.ExpiraEn.ShouldBe(s_ahora + s_duracion);
    }

    // El resumen es lo que se guarda; sin él, la fila no sirve para comprobar nada.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Emitir_SinResumen_Lanza(string hash) =>
        Should.Throw<ArgumentException>(() => TokenDeRefresco.Emitir(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), hash, s_ahora, s_duracion));

    [Fact]
    public void UnTokenCaducado_NoEstaVigente() =>
        Emitido().EstaVigente(s_ahora + s_duracion + TimeSpan.FromSeconds(1)).ShouldBeFalse();

    // ------------------------------------------------------------------------- Rotación

    [Fact]
    public void Canjear_LoMarcaYApuntaAlSustituto()
    {
        TokenDeRefresco token = Emitido();
        var sustituto = Guid.CreateVersion7();

        token.Canjear(sustituto, s_ahora.AddMinutes(10));

        token.EstaCanjeado.ShouldBeTrue();
        token.CanjeadoEn.ShouldBe(s_ahora.AddMinutes(10));
        token.SustituidoPorId.ShouldBe(sustituto);
    }

    // Un token canjeado no vuelve a valer NUNCA, aunque no haya caducado. Es la mitad de la
    // rotación: sin esto, el token robado sigue sirviendo hasta que expire por antigüedad.
    [Fact]
    public void UnTokenCanjeado_YaNoEstaVigenteAunqueNoHayaCaducado()
    {
        TokenDeRefresco token = Emitido();

        token.Canjear(Guid.CreateVersion7(), s_ahora.AddMinutes(10));

        token.EstaVigente(s_ahora.AddMinutes(11)).ShouldBeFalse();
    }

    // Canjearlo dos veces es exactamente la señal que hay que detectar. Que el agregado lance
    // deja el fallo donde se ve, en vez de dejar que se sobreescriba el primer canje y se pierda
    // la única huella de que había una copia por ahí.
    [Fact]
    public void Canjear_DosVeces_Lanza()
    {
        TokenDeRefresco token = Emitido();
        token.Canjear(Guid.CreateVersion7(), s_ahora.AddMinutes(10));

        Should.Throw<InvalidOperationException>(() =>
            token.Canjear(Guid.CreateVersion7(), s_ahora.AddMinutes(20)));
    }

    // ------------------------------------------------------------------------ Revocación

    [Fact]
    public void Revocar_LoInvalidaYGuardaElMotivo()
    {
        TokenDeRefresco token = Emitido();

        token.Revocar(MotivoDeRevocacion.ReutilizacionDetectada, s_ahora.AddMinutes(5));

        token.EstaVigente(s_ahora.AddMinutes(6)).ShouldBeFalse();
        token.RevocadoEn.ShouldBe(s_ahora.AddMinutes(5));
        token.Motivo.ShouldBe(MotivoDeRevocacion.ReutilizacionDetectada);
    }

    // El motivo es información de seguridad: la reutilización detectada no puede quedar tapada
    // por un cierre de sesión posterior en el mismo barrido.
    [Fact]
    public void Revocar_DosVeces_ConservaElPrimerMotivo()
    {
        TokenDeRefresco token = Emitido();
        token.Revocar(MotivoDeRevocacion.ReutilizacionDetectada, s_ahora);

        token.Revocar(MotivoDeRevocacion.CierreDeSesion, s_ahora.AddHours(1));

        token.Motivo.ShouldBe(MotivoDeRevocacion.ReutilizacionDetectada);
        token.RevocadoEn.ShouldBe(s_ahora);
    }

    // La familia es lo que permite revocar la cadena entera al detectar una reutilización: todas
    // las emisiones que descienden del mismo inicio de sesión la comparten.
    [Fact]
    public void LasEmisionesDeLaMismaCadena_CompartenFamilia()
    {
        var familia = Guid.CreateVersion7();

        TokenDeRefresco primero = Emitido(familia);
        TokenDeRefresco segundo = Emitido(familia);

        primero.FamiliaId.ShouldBe(familia);
        segundo.FamiliaId.ShouldBe(familia);
        primero.Id.ShouldNotBe(segundo.Id);
    }

    // La empresa activa viaja DENTRO de la emisión: renovar no es ocasión de cambiar de empresa.
    [Fact]
    public void ElTokenLlevaLaEmpresaActivaDeLaSesion()
    {
        var empresa = Guid.CreateVersion7();

        var token = TokenDeRefresco.Emitir(
            Guid.CreateVersion7(), Guid.CreateVersion7(), empresa, "resumen", s_ahora, s_duracion);

        token.EmpresaActivaId.ShouldBe(empresa);
    }
}
