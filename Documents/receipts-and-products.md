# Чеки и товары

Чек хранится в [Receipt](../CostVision.Domain/Models/Receipts/Receipt.cs). Он содержит фискальные реквизиты, дату покупки, сумму, магазин, позиции и связи со счетами. Методы `Try...` проверяют входные данные до изменения состояния: например, не допускают пустые фискальные номера или повторное добавление счёта.

Позиция чека — [ReceiptItem](../CostVision.Domain/Models/Receipts/ReceiptItem.cs). Товар — [Product](../CostVision.Domain/Models/Receipts/Product.cs), а магазин — [Store](../CostVision.Domain/Models/Receipts/Store.cs). Адаптивные названия товара и магазина позволяют исправить отображаемое имя, не теряя исходные данные чека.

Чек можно создать вручную через [SaveManualReceiptUseCase](../CostVision.Application/UseCases/Receipts/Receipts/SaveManualReceiptUseCase.cs), получить из QR-кода через [SaveReceiptsScannedUseCase](../CostVision.Application/UseCases/Receipts/Receipts/SaveReceiptsScannedUseCase.cs) или обновить по внешнему источнику через [RefreshReceiptFromApiUseCase](../CostVision.Application/UseCases/Receipts/Receipts/RefreshReceiptFromApiUseCase.cs). Внешний API скрыт за контрактом [IExternalReceiptProvider](../CostVision.Application/Abstractions/Service/Receipts/IExternalReceiptProvider.cs).

Если чек пока не удалось получить, фоновая служба [ReceiptRefreshBackgroundService](../CostVision.Infrastructure/Services/BackgroundServices/ReceiptRefreshBackgroundService.cs) периодически запускает [RefreshPendingReceiptsUseCase](../CostVision.Application/UseCases/Receipts/Receipts/RefreshPendingReceiptsUseCase.cs). Сама сущность хранит число попыток, следующую дату попытки и последнюю ошибку, поэтому обработка не повторяется бесконечно.
