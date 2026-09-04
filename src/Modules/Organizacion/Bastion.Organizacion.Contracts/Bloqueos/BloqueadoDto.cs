namespace Bastion.Organizacion.Contracts.Bloqueos;

/// <summary>
/// Un recurso bloqueado, tal como sale del único camino que enseña lo bloqueado (ADR-0027).
/// </summary>
/// <remarks>
/// <para>
/// <b>No lleva testigo de versión, y su ausencia es la pieza que sostiene cuatro exenciones.</b>
/// Las cuatro acciones de desbloqueo están exentas de <c>If-Match</c> porque la etiqueta se
/// consigue leyendo el recurso y un recurso bloqueado no se dejaba leer (ADR-0017). Este listado
/// rompe la mitad literal de esa frase —ya hay una lectura que entrega lo bloqueado— y deja en pie
/// la que de verdad importa: <b>sigue sin haber etiqueta</b>. El día que alguien añada aquí un
/// <c>Version</c>, un <c>ETag</c> o un <c>Xmin</c> «porque el listado ya lo tiene a mano», la llave
/// vuelve a existir y las cuatro exenciones caducan a la vez. Por eso no depende de que alguien lo
/// recuerde: lo vigilan <c>NingunaLecturaEntregaTestigoDeVersionTests</c> sobre el contrato entero
/// de la API y la regla de caminos disjuntos de <c>ElFiltroNoSeSaltaPorAhiTests</c>.
/// </para>
/// <para>
/// <b>Lleva la fecha de bloqueo y la de vencimiento, no solo la primera.</b> El art. 32 de la
/// LOPDGDD reserva los datos «durante el plazo de prescripción», y un bloqueo sin fin convierte una
/// conservación acotada en indefinida — que es otra infracción, por el otro lado. Quien administra
/// bloqueos tiene que poder ver cuál ha vencido sin calcularlo de cabeza.
/// </para>
/// <para>
/// <b>Los enumerados viajan como texto</b>, igual que en el resto del módulo: por el ordinal, el
/// día que alguien reordene un enumerado los clientes ya desplegados leerían otro valor sin que
/// nada fallara.
/// </para>
/// </remarks>
/// <param name="Id">Identificador del recurso bloqueado. Es lo que pide su desbloqueo.</param>
/// <param name="Tipo">Qué es: <c>Empresa</c>, <c>Almacen</c> o <c>Ubicacion</c>, como texto.</param>
/// <param name="Codigo">
/// Código del recurso, o nulo si su tipo no tiene: una empresa se identifica por su razón social y
/// no por un código. El nulo dice «este tipo no tiene código», no «no se ha podido leer».
/// </param>
/// <param name="Nombre">Con qué nombre se le reconoce: razón social, nombre o descripción.</param>
/// <param name="BloqueadoEn">Cuándo se bloqueó. De aquí arranca el plazo.</param>
/// <param name="Motivo">Por qué se bloqueó, como texto.</param>
/// <param name="VenceEn">
/// Cuándo termina la reserva, o nulo si <b>no vence</b>. El nulo es información y no un hueco: un
/// almacén retirado se conserva por razón contable y sus datos no son de nadie. Quien lo pinte
/// tiene que escribir «no vence» y no dejar la celda vacía.
/// </param>
public sealed record BloqueadoDto(
    Guid Id,
    string Tipo,
    string? Codigo,
    string Nombre,
    DateTimeOffset BloqueadoEn,
    string Motivo,
    DateTimeOffset? VenceEn);
