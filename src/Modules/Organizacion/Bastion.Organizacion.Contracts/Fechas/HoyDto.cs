using Bastion.BuildingBlocks.Contracts.Fechas;

namespace Bastion.Organizacion.Contracts.Fechas;

/// <summary>Hoy en la empresa activa y en cada zona admitida, tal como sale de la API.</summary>
/// <param name="DeLaEmpresa">
/// Hoy en la zona de la empresa: el de lo que no tiene almacén, como las tarifas o los saldos sin
/// fecha.
/// </param>
/// <param name="ElMasAdelantado">
/// Hoy en la más adelantada de las zonas que rigen en la empresa: el tope de una fecha que pudo
/// ser hoy en cualquiera de sus almacenes, como la de los saldos.
/// </param>
/// <param name="EnCadaZona">
/// Hoy en cada zona admitida, para el día de un almacén por su zona efectiva.
/// </param>
/// <remarks>
/// <para>
/// <b>El frontal no calcula el día: lo lee de aquí</b> (ADR-0079 §12), y así la regla vive en un
/// solo sitio, <c>DiaDeNegocio</c>, con el reloj del servidor.
/// </para>
/// <para>
/// <b>Todo sale del mismo instante</b>, así que los días no se contradicen entre ellos. Y cada zona
/// dice cuándo acaba su día, que es cuando hay que volver a preguntar.
/// </para>
/// </remarks>
public sealed record HoyDto(DateOnly DeLaEmpresa, DateOnly ElMasAdelantado, IReadOnlyList<HoyEnUnaZonaDto> EnCadaZona);

/// <summary>Hoy en una zona, y el instante en que deja de serlo.</summary>
/// <param name="Zona">La zona.</param>
/// <param name="Dia">El día.</param>
/// <param name="AcabaEn">El principio del día siguiente en la misma zona, en UTC.</param>
public sealed record HoyEnUnaZonaDto(ZonaHorariaDto Zona, DateOnly Dia, DateTimeOffset AcabaEn);
