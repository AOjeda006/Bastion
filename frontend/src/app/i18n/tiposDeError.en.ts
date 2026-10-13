import type { TiposDeError } from './tiposDeError.es.ts';

/**
 * English. The type does the checking, as in `en.ts`: a `type` without its text here —or one that
 * nobody removed from here after removing it from `tiposDeError.es.ts`— is a compile error.
 */
export const tiposDeError: TiposDeError = {
  // Ver `tiposDeError.es.ts`: las doce del ajuste, y las cuatro de un maestro bloqueado o retirado
  // con sus dos mitades.
  'ajuste-almacen-bloqueado':
    'That warehouse is blocked: its earlier movements are still read and valued, but it does ' +
    'not accept a new adjustment. Pick another warehouse or ask for it to be unblocked.',
  'ajuste-almacen-no-encontrado':
    'That warehouse does not exist. Pick one from the warehouse list.',
  'ajuste-articulo-no-encontrado': 'That item does not exist. Pick one from the item list.',
  'ajuste-articulo-no-se-almacena':
    'That item is a service: it has no stock to adjust. Remove that line or change the item.',
  'ajuste-con-fecha-futura':
    'That adjustment is dated after today: the ledger only records what has already ' +
    'happened. Confirm it on that day or fix the date.',
  'ajuste-coste-no-valido':
    'A line that takes stock out carries no cost: it is valued at the average cost. And no ' +
    'cost can be negative; a sample or a gift comes in at zero.',
  'ajuste-de-un-recuento-no-se-anula':
    'That adjustment is the difference of a stock count, and it is not reversed on its own: ' +
    'the count would stay confirmed with its difference undone. Reverse the stock count, and ' +
    'its adjustment will be reversed with it.',
  'ajuste-en-ejercicio-cerrado':
    'That adjustment is dated inside a closed financial year: the period is already final ' +
    'and takes no new documents. Change the date or ask for the year to be reopened.',
  'ajuste-entrada-sin-coste-ni-precio-medio':
    'A line brings in an item that has no stock in that warehouse, and with no stock there is ' +
    'no average cost to value it at. Enter the cost of that line.',
  'ajuste-fecha-anterior-al-ultimo-movimiento':
    'An item in this document already has a later movement in that warehouse, and a document ' +
    'cannot be dated before the last movement of its item. Use that date or a later one.',
  'ajuste-inverso-no-se-anula':
    'That adjustment is the counter-document of another, and a counter-document is not ' +
    'undone with another: undoing it would be making the adjustment again. If needed, make a ' +
    'new adjustment.',
  'ajuste-lote-no-valido':
    'That batch is not a code that fits on the label: 1 to 20 characters, no spaces, using ' +
    'unaccented letters, digits and the symbols GS1 allows. Fix the batch on that line.',
  'ajuste-motivo-no-valido':
    'Write why you are reversing the adjustment, in 300 characters or fewer. It is the only ' +
    'thing left to explain the correction two years from now.',
  'ajuste-no-encontrado': 'That adjustment no longer exists. Go back to the list and refresh.',
  'ajuste-no-esta-confirmado':
    'That adjustment is not confirmed, so there is nothing to reverse: a draft has not moved ' +
    'the ledger. Refresh the screen to see what state it is in.',
  'ajuste-no-esta-en-borrador':
    'That adjustment is no longer a draft: a confirmed one is not confirmed twice, because ' +
    'its ledger rows are already written and the ledger is never rewritten. Refresh the screen.',
  'ajuste-numero-de-serie-no-valido':
    'That serial number is not a code that fits on the label: 1 to 20 characters, no spaces, ' +
    'using unaccented letters, digits and the symbols GS1 allows. Fix the serial number on ' +
    'that line.',
  'ajuste-serie-cerrada':
    'That series is closed: it still resolves the documents it already numbered, but it hands ' +
    'out no further numbers. Choose another series.',
  'ajuste-serie-no-encontrada': 'That series does not exist. Pick one from the series list.',
  'ajuste-serie-no-unitaria':
    'A line with a serial number moves exactly one unit, because each number is one piece and ' +
    'no two share it. Use one line per piece, with a quantity of 1 in the item’s base unit.',
  'ajuste-serie-repetida':
    'Two lines of the adjustment name the same serial number, and each piece appears only ' +
    'once per document. Moving it to another location is a relocation, which is a different ' +
    'document.',
  'ajuste-sin-ejercicio':
    'That adjustment is dated outside every financial year: with no period to book it to, ' +
    'the document would not reach any return. Open the missing year or fix the date.',
  'ajuste-sin-lineas':
    'An adjustment needs at least one line: a document that moves nothing adjusts nothing.',
  'ajuste-trazabilidad-no-casa':
    'A line does not match its item’s traceability: it lacks the batch or serial number the ' +
    'item requires, or carries one the item does not track. Fix the line, or the item’s ' +
    'traceability if it has not moved yet.',
  'ajuste-ubicacion-bloqueada':
    'That location is blocked: whatever is already assigned to it is still read, but nothing ' +
    'new moves into that slot. Pick another location.',
  'ajuste-ubicacion-no-encontrada':
    'That location does not exist in that warehouse. Pick one of its own.',
  'ajuste-unidad-no-encontrada':
    'That unit of measure does not exist. Pick one from the unit list.',
  'ajuste-unidad-retirada':
    'That unit has been withdrawn: movements already written in it are still read, but a new ' +
    'one is not written with it. Pick another unit.',
  'ajuste-valoracion-en-otra-divisa':
    'The stock of an item in this document is valued in another currency in that warehouse, ' +
    'and adding them up would need an exchange rate. Make the adjustment in the currency the ' +
    'stock is valued in.',
  'almacen-duplicado': 'There is already a warehouse with that code at this company.',
  'almacen-no-encontrado': 'That warehouse no longer exists. Go back to the list and refresh.',
  'articulo-duplicado': 'There is already an item with that code at this company.',
  'articulo-impuesto-no-encontrado': 'That tax does not exist. Pick one from the tax list.',
  'articulo-impuesto-no-vigente':
    'That tax period no longer applies. Pick one that is currently in force.',
  'articulo-no-encontrado': 'That item no longer exists. Go back to the list and refresh.',
  'articulo-proveedor-duplicado': 'That supplier is already on this item’s list.',
  'articulo-proveedor-no-encontrado':
    'That supplier is no longer on this item’s list. Refresh the screen.',
  // Same wording decision as in Spanish: three situations, one answer, on purpose.
  'articulo-proveedor-tercero-no-valido':
    'That business partner cannot be added as a supplier of this item. Check in the ' +
    'business partners master that the record exists and is marked as a supplier.',
  'articulo-servicio-con-trazabilidad':
    'A service has no stock, so it carries neither a batch nor a serial number.',
  'articulo-tipo-no-valido': 'An item can only be of type “Bien” or “Servicio”.',
  'articulo-trazabilidad-con-movimientos':
    'This item already has stock movements, so its traceability can no longer change.',
  'articulo-trazabilidad-no-valida':
    'An item’s traceability can only be none, by batch or by serial number.',
  'articulo-unidad-no-encontrada':
    'That unit of measure does not exist. Pick one from the unit list.',
  'articulo-unidad-retirada':
    'That unit has been withdrawn: items already using it are still counted in it, but a ' +
    'new one cannot be registered with it. Pick another one.',
  'categoria-ciclo':
    'A category cannot hang from itself, nor from any of the ones hanging from it.',
  'categoria-demasiado-profunda':
    'The category tree does not allow any more levels below that place.',
  'categoria-duplicada': 'There is already a category with that code at this company.',
  'categoria-no-encontrada': 'That category no longer exists. Go back to the list and refresh.',
  'categoria-padre-no-encontrado': 'The category you want to hang this one from does not exist.',
  'codigo-barras-duplicado':
    'An item at this company already carries that GTIN. If it now belongs to another one, remove it from the first.',
  'codigo-barras-nivel-no-valido': 'A barcode goes on the base unit, on a case or on a pallet.',
  'codigo-barras-no-encontrado':
    'That barcode is no longer on this item: someone removed it first.',
  'codigo-barras-unidades-no-validas':
    'The base unit holds one unit. A case or a pallet holds the base units inside it, which ' +
    'are two or more.',
  'codigo-de-rol-ya-usado': 'There is already a role with that code. Pick another one.',
  'contrasena-actual-incorrecta': 'Your current password is not correct.',
  'conversion-um-duplicada': 'There is already a conversion between those two units.',
  'conversion-um-inversa-implausible':
    'The opposite direction is already declared and that factor is not its inverse.',
  'conversion-um-no-declarada': 'No conversion is declared between those two units. Add one.',
  'conversion-um-no-encontrada': 'That unit conversion no longer exists.',
  'correo-ya-registrado': 'There is already an account with that email address.',
  'credenciales-no-validas': 'The email address or the password is not correct.',
  'cuenta-bancaria-duplicada': 'This record already has that bank account.',
  'cuenta-bancaria-no-encontrada': 'This record has no such bank account.',
  'cuerpo-demasiado-grande':
    'What you are trying to send is too large. Split it into smaller sends.',
  'datos-no-validos': 'Some fields are not valid. Check the ones marked below.',
  'divisa-duplicada': 'There is already a currency with that code.',
  'divisa-no-encontrada': 'That currency no longer exists.',
  'ejercicio-cerrado': 'The financial year is closed and cannot be changed.',
  'ejercicio-con-borradores':
    'There are still draft documents dated inside the financial year. Confirm or delete them before closing.',
  'ejercicio-con-documentos':
    'The financial year has documents dated inside it. Deleting it would leave them with no financial year to belong to.',
  'ejercicio-con-series':
    'A financial year with document series cannot be deleted. Delete the series first.',
  'ejercicio-dejaria-documentos-fuera':
    'Those dates would leave documents that fall inside today outside the financial year. Move the documents first, or leave the range where it is.',
  'ejercicio-duplicado': 'There is already a financial year with that year number at this company.',
  'ejercicio-motivo-no-valido':
    'Say why you are reopening the financial year: it is all that will be left to explain it.',
  'ejercicio-no-encontrado':
    'That financial year no longer exists. Go back to the list and refresh.',
  'ejercicio-solapado':
    'Those dates overlap another financial year at this company. A date must fall in exactly one financial year.',
  'ejercicio-ya-abierto': 'That financial year was already open, so it has not been reopened.',
  'ejercicio-ya-cerrado':
    'That financial year was already closed, so you are not the one who closed it. Read it again before deciding.',
  'empresa-activa-no-operativa':
    'The company you are working with is no longer available. Sign in again.',
  'empresa-ajena': 'That company is not yours, so you cannot work on it.',
  'empresa-destino-no-operativa':
    'The company you picked does not accept new people: it does not exist or it is blocked.',
  'empresa-no-encontrada': 'That company no longer exists. Go back to the list and refresh.',
  'empresa-no-pertenece': 'You do not belong to that company, so you cannot work with it.',
  'empresa-ya-registrada': 'There is already a company with that tax number.',
  'falta-if-match':
    'Saving requires saying which version you are writing over. Open the form again.',
  'fecha-fuera-del-ejercicio-de-la-serie':
    'That series belongs to another financial year: it does not number documents with this ' +
    'date. Open the adjustment with a series from the year of its date.',
  'gtin-circulacion-restringida':
    'That is a restricted circulation number: a shop or a country assigns it for internal ' +
    'use, and outside that scope it does not identify an item.',
  'gtin-cupon': 'That is the number of a coupon or a refund voucher, not of an item.',
  'gtin-digito-de-control':
    'The last digit does not match the others: the number was misread or mistyped.',
  'gtin-largo-no-admitido':
    'A GTIN has 8, 12, 13 or 14 digits. If you count a different number, look at the code ' +
    'again: one is missing or extra.',
  'gtin-medida-variable':
    'That is a variable measure code: without the measure it is not complete, and it does ' +
    'not identify an item.',
  'gtin-no-son-digitos': 'A GTIN is digits only: no letters, dashes or spaces inside.',
  'gtin-sin-asignar':
    'That prefix is reserved, or belongs to something that is not an item, so it is not ' +
    'the GTIN of any item.',
  'idempotencia-clave-no-valida':
    'The application sent a retry key that is not valid. Please try again.',
  'idempotencia-cuerpo-distinto':
    'An operation was retried with the same send key but different content. Start again.',
  'idempotencia-no-admitida': 'This operation cannot be retried safely. Please try again.',
  'idempotencia-obligatoria':
    'This operation only runs with a retry key, because stopping halfway would leave a gap in ' +
    'the numbering. Please try again.',
  'idempotencia-sin-empresa-activa':
    'Your session has no active company. Choose one and try again.',
  'if-match-no-valido': 'The version the form carried is not well formed. Open it again.',
  'importacion-cabecera-no-valida':
    'The first row of the file is not the template header. Copy it exactly, in its order.',
  'importacion-codificacion-no-admitida':
    'The file is not saved as an Excel CSV. Save it as "CSV (Comma delimited)" or "CSV UTF-8".',
  'importacion-comillas-sin-cerrar':
    'The file has a quote that opens and never closes. Check it in the spreadsheet.',
  'importacion-demasiadas-filas':
    'The file has too many rows. Split it into files of 5000 rows at most.',
  'importacion-fin-de-linea-no-admitido':
    'The file separates rows in a way we do not accept. Open it and save it again as CSV.',
  'importacion-separador-no-admitido':
    'The file does not separate columns with semicolons. Save it from Excel with Spanish (Spain) regional settings.',
  'importacion-sin-permiso-de-alta':
    'Importing third parties means creating them, and you are not allowed to create third parties.',
  'importacion-sin-permiso-de-limite':
    'Some rows carry a credit limit and you are not allowed to set it. Leave those columns empty.',
  'impuesto-con-tramos-solapados': 'The validity ranges of that tax overlap. Check the dates.',
  'impuesto-no-encontrado': 'That tax no longer exists.',
  'numero-de-serie-en-existencias':
    'A serial number in this document is already in stock: each piece can only be in one ' +
    'place, and only once. Nothing has been saved. If the piece has moved, that is a ' +
    'relocation, not an entry.',
  'orden-no-admitido': 'That field cannot be used for sorting.',
  'permisos-de-rol-del-sistema':
    'The permissions of the system role are set by every update. You can rename it; to grant fewer permissions, create a role of your own.',
  'pertenencia-no-encontrada': 'That person does not belong to the company you named.',
  'recuento-almacen-bloqueado':
    'That warehouse is blocked: its stock can still be read, but it does not accept a new ' +
    'stock count. Ask for it to be unblocked.',
  'recuento-almacen-no-encontrado':
    'That warehouse does not exist. Pick one from the warehouse master.',
  'recuento-articulo-no-encontrado': 'That item does not exist. Pick one from the item list.',
  'recuento-articulo-no-se-almacena':
    'That item is a service: it has no stock to count. Pick an item that is kept in stock.',
  'recuento-clave-repetida':
    'The stock count already has that key: the same location, the same item and the same ' +
    'batch or serial number. Find it among the lines and count it there.',
  'recuento-con-lineas-sin-contar':
    'Some lines are still uncounted, and an uncounted line is not a zero. Count them, or ' +
    'remove the ones you will not count: their stock will stay as it is.',
  'recuento-contado-no-valido':
    'The counted quantity is in the item’s base unit: it is not negative, has at most six ' +
    'decimals and, for a serial number, is 0 or 1. Fix the figure.',
  'recuento-coste-no-valido':
    'The cost of one unit cannot be negative or have more than fourteen integer digits. Fix ' +
    'it, or leave it empty if you do not know it.',
  'recuento-en-ejercicio-cerrado':
    'Today falls inside a closed financial year, and what the stock count writes carries ' +
    'today’s date: its adjustment when confirming and the counter-document when reversing. The ' +
    'period is already final and takes no new documents. Ask for the year to be reopened.',
  'recuento-linea-no-encontrada':
    'That line is no longer in the stock count: someone removed it first. Go back to the count and refresh.',
  'recuento-lote-no-valido':
    'That batch is not a code that fits on the label: 1 to 20 characters, no spaces, using ' +
    'unaccented letters, digits and the symbols GS1 allows. Fix the batch.',
  'recuento-motivo-no-valido':
    'Write the reason, in 300 characters or fewer. It is all that will be left to understand ' +
    'why the stock was counted, reversed or discarded.',
  'recuento-no-encontrado': 'That stock count no longer exists. Go back to the list and refresh.',
  'recuento-no-esta-confirmado':
    'That stock count is not confirmed, and only a confirmed one can be reversed: one in ' +
    'progress has moved nothing and is discarded, and one reversed or discarded is already ' +
    'closed. Refresh the screen to see its state.',
  'recuento-no-esta-en-curso':
    'That stock count is no longer in progress: it was confirmed, reversed or discarded, and ' +
    'what was counted no longer changes. Refresh the screen to see its state.',
  'recuento-numero-de-serie-no-valido':
    'That serial number is not a code that fits on the label: 1 to 20 characters, no spaces, ' +
    'using unaccented letters, digits and the symbols GS1 allows. Fix the serial number.',
  'recuento-serie-cerrada':
    'One of the two series is closed: the count series or the adjustment series. Pick another.',
  'recuento-serie-de-otro-documento':
    'One of the two series numbers another document: the count series must be for stock ' +
    'counts, and the adjustment series for adjustments. Pick the right one for each.',
  'recuento-serie-de-un-ejercicio-terminado':
    'One of the two series belongs to a fiscal year that has already ended, and the stock ' +
    'count is numbered with the date it is confirmed. Pick a series from the current year.',
  'recuento-serie-no-encontrada':
    'One of the two series does not exist: the count series or the adjustment series. Pick one from the series master.',
  'recuento-serie-repetida':
    'That serial number is already in the stock count, in another location, and each piece is ' +
    'counted only once. Count it on the line that already has it, or remove that line first.',
  'recuento-sin-ejercicio':
    'Today falls outside every financial year, and what the stock count writes carries ' +
    'today’s date: its adjustment when confirming and the counter-document when reversing. ' +
    'Open the missing year and try again.',
  'recuento-sube-con-transito':
    'Some lines were counted above the system figure in locations with goods in transit ' +
    'towards them: if those goods have arrived but were not received, they would be added ' +
    'twice. Receive the transfers and confirm again.',
  'recuento-teorico-cambiado':
    'The warehouse has changed since you opened the stock count: someone moved goods on the ' +
    'highlighted lines. Review them, recount them if needed and confirm again.',
  'recuento-trazabilidad-no-casa':
    'That key does not match the item’s traceability: it lacks the batch or serial number the ' +
    'item requires, or carries one the item does not track. Fix the key.',
  'recuento-ubicacion-bloqueada':
    'That location is blocked: whatever is already assigned to it is still read, but nothing ' +
    'new is added in that slot. Pick another location.',
  'recuento-ubicacion-no-encontrada':
    'That location does not exist in the stock count’s warehouse. Pick one of its own.',
  'recuento-ya-hay-uno-en-curso':
    'That warehouse is already being counted. Confirm or discard the count in progress before opening another.',
  'reserva-almacen-bloqueado':
    'That warehouse is blocked: its stock is still read, but it does not accept a new ' +
    'reservation. Pick another warehouse or ask for it to be unblocked.',
  'reserva-almacen-no-encontrado':
    'That warehouse does not exist. Pick one from the warehouse list.',
  'reserva-articulo-no-encontrado': 'That item does not exist. Pick one from the item list.',
  'reserva-articulo-no-se-almacena':
    'That item is a service: it has no stock to set aside. Remove that line or change the item.',
  'reserva-caducada':
    'That reservation has expired, and what it set aside is available again: it can no ' +
    'longer be consumed or released. Review the document that asked for it.',
  'reserva-caducidad-no-valida':
    'The expiry must be later than now: a reservation that expires as it is made sets nothing ' +
    'aside. Use a later date, or leave it without an expiry.',
  'reserva-cantidad-no-valida':
    'The quantity is in the item’s base unit: positive and with at most six decimals. Fix ' +
    'the figure.',
  'reserva-con-fecha-futura':
    'That date is after today: the ledger only records what has already happened, and goods ' +
    'leave on the day they leave. Do it on that day or fix the date.',
  'reserva-consumo-por-encima-de-lo-pendiente':
    'The lines take out more than the reservation has left, and anything beyond that is not ' +
    'set aside for this document. Take out at most what is pending.',
  'reserva-consumo-sin-stock':
    'A line takes more out of a location than it holds with that batch or serial number. ' +
    'Pick another location or batch, or take out less.',
  'reserva-documento-no-valido':
    'What consumes a reservation is a delivery note, and it must say which one. Name the ' +
    'delivery note.',
  'reserva-documento-ya-la-consumio':
    'That delivery note has already consumed this reservation, and a delivery note consumes ' +
    'it only once. If something is still pending, take it out with another delivery note.',
  'reserva-en-ejercicio-cerrado':
    'That date falls inside a closed financial year: the period is already final and takes ' +
    'no new documents. Change the date or ask for the year to be reopened.',
  'reserva-fecha-anterior-al-ultimo-movimiento':
    'The item already has a later movement in that warehouse, and an outgoing movement cannot ' +
    'be dated before the last movement of its item. Use that date or a later one.',
  'reserva-lote-no-valido':
    'That batch is not a code that fits on the label: 1 to 20 characters, no spaces, using ' +
    'unaccented letters, digits and the symbols GS1 allows. Fix the batch on that line.',
  'reserva-motivo-no-valido':
    'Write why you are releasing the reservation, in 300 characters or fewer. It is the only ' +
    'thing left to explain why what it set aside was let go.',
  'reserva-no-encontrada': 'That line has no reservation. Go back to the document and refresh.',
  'reserva-no-esta-activa':
    'That reservation is no longer active: it was fully consumed or released, and it sets ' +
    'nothing aside. Refresh the screen to see what state it is in.',
  'reserva-numero-de-serie-no-valido':
    'That serial number is not a code that fits on the label: 1 to 20 characters, no spaces, ' +
    'using unaccented letters, digits and the symbols GS1 allows. Fix the serial number on ' +
    'that line.',
  'reserva-origen-con-otra-reserva':
    'That line already has its reservation, and you are asking for something else: another ' +
    'item, warehouse, quantity or expiry. A line reserves only once, so the one it has stands.',
  'reserva-origen-no-valido':
    'The reservation must say which document and which line it comes from: without that, a ' +
    'retry would not recognise it and would set stock aside twice.',
  'reserva-por-encima-del-disponible':
    'Not that much is available: what is left in that warehouse, minus what is already ' +
    'reserved, does not reach what you ask for. Reserve less, or in another warehouse.',
  'reserva-serie-no-entera':
    'That item is tracked by serial number, and each number is one piece: only whole pieces ' +
    'can be reserved. Ask for a quantity with no decimals in the item’s base unit.',
  'reserva-serie-no-unitaria':
    'A line with a serial number takes out exactly one unit, because each number is one piece ' +
    'and no two share it. Use one line per piece, with a quantity of 1 in the item’s base unit.',
  'reserva-serie-repetida':
    'Two lines of the delivery note name the same serial number, and each piece leaves only ' +
    'once per document. Remove one of the two lines.',
  'reserva-sin-ejercicio':
    'That date falls outside every financial year: with no period to book it to, the ' +
    'outgoing movement would not reach any return. Open the missing year or fix the date.',
  'reserva-sin-lineas':
    'Consuming a reservation needs at least one line: a delivery note that takes nothing out ' +
    'serves none of it.',
  'reserva-trazabilidad-no-casa':
    'A line does not match the item’s traceability: it lacks the batch or serial number the ' +
    'item requires, or carries one the item does not track. Fix the line.',
  'reserva-ubicacion-bloqueada':
    'That location is blocked, or its warehouse is: what it holds can still be seen, but ' +
    'nothing new leaves it. Take the goods from another location, or unblock it.',
  'reserva-valoracion-en-otra-divisa':
    'The item’s stock in that warehouse is valued in another currency, and taking it out ' +
    'would need an exchange rate. It cannot leave while the warehouse values it that way.',
  'rol-no-encontrado': 'That role no longer exists. Go back to the list and refresh.',
  'serie-cerrada': 'The document series is closed and cannot be changed.',
  'serie-de-otro-documento':
    'That series numbers another kind of document. Open the adjustment with an inventory ' +
    'adjustment series.',
  'serie-duplicada': 'There is already a series with that code in that financial year.',
  'serie-no-encontrada': 'That series no longer exists. Go back to the list and refresh.',
  'serie-no-numera':
    'That series no longer hands out numbers: it was closed or deleted while your draft was ' +
    'open. Open the adjustment with another series.',
  'serie-ya-numerada': 'The series has already numbered documents, so that cannot be changed.',
  'sesion-no-renovable': 'Your session could not be renewed. Sign in again.',
  'stock-insuficiente':
    'There is not enough stock in one of the locations on this document, and nothing was ' +
    'saved. If you are cancelling a receipt, its units have already gone out: cancel those ' +
    'issues first or record the missing receipt.',
  'tarifa-divisa-no-encontrada': 'That currency does not exist. Pick one from the currency list.',
  'tarifa-divisa-retirada':
    'That currency has been withdrawn: price lists already using it still work, but a new ' +
    'one cannot be opened with it. Pick another one.',
  'tarifa-linea-articulo-o-categoria':
    'A price list line sets the price of an item or of a category, and you have to pick one ' +
    'of the two: not both, not neither.',
  'tarifa-linea-no-encontrada':
    'That price list line no longer exists. Go back to the list and refresh.',
  'tarifa-linea-precio-o-descuento':
    'A price list line carries a price or a discount, and you have to set one of the two: ' +
    'not both, not neither.',
  'tarifa-linea-primer-tramo-sin-cero':
    'The first quantity band has to start at zero. Starting higher would leave the ' +
    'quantities below it with no price.',
  'tarifa-linea-tramo-duplicado':
    'There is already a band starting at that quantity for that item or that category.',
  'tarifa-no-encontrada': 'That price list no longer exists. Go back to the list and refresh.',
  'tarifa-no-vigente':
    'That price list exists, but none of its periods covers the date asked for. Open the ' +
    'missing period or ask for another date.',
  'tarifa-sin-linea-aplicable':
    'That price list says nothing about that item at that quantity, neither its own line nor ' +
    'any of its categories. Add the missing line: there is no price to apply here.',
  'tarifa-vigencia-al-reves': 'The period ends before it starts. Check both dates.',
  'tarifa-vigencias-solapadas':
    'There is already another period of that price list covering some of those days. The ' +
    'periods of a price list cannot overlap.',
  'tercero-duplicado': 'This company already has a business partner with that tax identifier.',
  'tercero-no-encontrado':
    'That business partner no longer exists. Go back to the list and refresh.',
  'tercero-tarifa-no-encontrada':
    'That price list does not exist. Choose one from the price lists master.',
  'tercero-tarifa-no-vigente':
    'That price list is no longer in force: it still applies to whatever was issued while ' +
    'it ran, but it cannot be assigned today. Choose one that is in force.',
  'tipo-cambio-duplicado': 'There is already an exchange rate for that currency on that date.',
  'tipo-cambio-no-encontrado': 'That exchange rate no longer exists.',
  // Ver `tiposDeError.es.ts`: las veintinueve de la transferencia, que dicen qué punta del viaje
  // falla.
  'transferencia-almacen-bloqueado':
    'That warehouse is blocked: its earlier movements are still read and valued, but it does ' +
    'not accept a new transfer, either out or in. Pick another warehouse or ask for it to be ' +
    'unblocked.',
  'transferencia-almacen-no-encontrado':
    'That warehouse does not exist. Pick one from the warehouse list.',
  'transferencia-articulo-no-encontrado': 'That item does not exist. Pick one from the item list.',
  'transferencia-articulo-no-se-almacena':
    'That item is a service: it has no stock to carry from one warehouse to another. Remove ' +
    'that line or change the item.',
  'transferencia-cantidad-no-valida':
    'A line carries nothing from origin to destination: the quantity and the base-unit factor ' +
    'must be positive, and their product cannot round to zero. Fix the quantity on that line.',
  'transferencia-con-fecha-futura':
    'That date is after today: the ledger only records what has already happened, and a ' +
    'transfer is sent and received on the day it happens. Do it on that day or fix the date.',
  'transferencia-en-ejercicio-cerrado':
    'That date falls inside a closed financial year: the period is already final and takes ' +
    'no new documents. Change the date or ask for the year to be reopened.',
  'transferencia-fecha-anterior-al-ultimo-movimiento':
    'An item in this document already has a later movement in one of the two warehouses, and ' +
    'a document cannot be dated before the last movement of its item. Use that date or a ' +
    'later one.',
  'transferencia-lote-no-valido':
    'That batch is not a code that fits on the label: 1 to 20 characters, no spaces, using ' +
    'unaccented letters, digits and the symbols GS1 allows. Fix the batch on that line.',
  'transferencia-mismo-almacen':
    'The origin and the destination are the same warehouse, and that is not a transfer. ' +
    'Moving goods to another slot inside a warehouse is a relocation, which is a different ' +
    'document.',
  'transferencia-motivo-no-valido':
    'Write why you are reversing the transfer, in 300 characters or fewer. It is the only ' +
    'thing left to explain the correction two years from now.',
  'transferencia-no-encontrada': 'That transfer no longer exists. Go back to the list and refresh.',
  'transferencia-no-esta-en-borrador':
    'That transfer is no longer a draft: a sent one is not sent twice, because what left the ' +
    'origin is already written in the ledger. Refresh the screen.',
  'transferencia-no-esta-enviada':
    'That transfer is not in transit: a draft has not left the origin yet, and a received or ' +
    'reversed one is no longer on its way. Refresh the screen to see what state it is in.',
  'transferencia-no-se-anula':
    'That transfer cannot be reversed: a draft has not moved the ledger, a reversed one ' +
    'already has its counter-document, and a counter-document is not undone with another. ' +
    'Refresh the screen to see what state it is in.',
  'transferencia-numero-de-serie-no-valido':
    'That serial number is not a code that fits on the label: 1 to 20 characters, no spaces, ' +
    'using unaccented letters, digits and the symbols GS1 allows. Fix the serial number on ' +
    'that line.',
  'transferencia-por-encima-del-disponible':
    'Part of that stock is reserved for other orders, and a transfer cannot take it: what is ' +
    'left in the origin warehouse, minus what is reserved, does not reach what you send. Send ' +
    'less, or release the reservation first.',
  'transferencia-recepcion-antes-del-envio':
    'The receipt date is earlier than the dispatch date, and nothing arrives before it ' +
    'leaves. Use the dispatch date or a later one.',
  'transferencia-serie-cerrada':
    'That series is closed: it still resolves the documents it already numbered, but it hands ' +
    'out no further numbers. Choose another series.',
  'transferencia-serie-no-encontrada': 'That series does not exist. Pick one from the series list.',
  'transferencia-serie-no-unitaria':
    'A line with a serial number moves exactly one unit, because each number is one piece and ' +
    'no two share it. Use one line per piece, with a quantity of 1 in the item’s base unit.',
  'transferencia-serie-repetida':
    'Two lines of the transfer name the same serial number, and each piece travels only once ' +
    'per document. Remove one of the two lines.',
  'transferencia-sin-ejercicio':
    'That date falls outside every financial year: with no period to book it to, the ' +
    'document would not reach any return. Open the missing year or fix the date.',
  'transferencia-sin-lineas':
    'A transfer needs at least one line: a document that moves nothing carries nothing from ' +
    'one warehouse to another.',
  'transferencia-trazabilidad-no-casa':
    'A line does not match its item’s traceability: it lacks the batch or serial number the ' +
    'item requires, or carries one the item does not track. Fix the line, or the item’s ' +
    'traceability if it has not moved yet.',
  'transferencia-ubicacion-bloqueada':
    'That location is blocked: whatever is already assigned to it is still read, but nothing ' +
    'new moves out of that slot or into it. Pick another location.',
  'transferencia-ubicacion-no-encontrada':
    'That location does not exist in that warehouse. Pick one of its own.',
  'transferencia-unidad-no-encontrada':
    'That unit of measure does not exist. Pick one from the unit list.',
  'transferencia-unidad-retirada':
    'That unit has been withdrawn: movements already written in it are still read, but a new ' +
    'one is not written with it. Pick another unit.',
  'transferencia-valoracion-en-otra-divisa':
    'The stock of an item in this document is valued in another currency in one of the two ' +
    'warehouses, and putting them together would need an exchange rate. The transfer cannot ' +
    'be made while that warehouse values it that way.',
  'ubicacion-duplicada': 'There is already a location with that code in that warehouse.',
  'ubicacion-no-encontrada': 'That location no longer exists. Go back to the list and refresh.',
  'unidad-medida-duplicada': 'There is already a unit of measure with that code.',
  'unidad-medida-no-encontrada': 'That unit of measure no longer exists.',
  'usuario-no-encontrado': 'That person no longer exists. Go back to the list and refresh.',
  'version-obsoleta':
    'Someone saved before you did. Open the form again so you do not overwrite their changes.',
};
