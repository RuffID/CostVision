# Денежные операции

Денежная операция — [MoneyMovement](../CostVision.Domain/Models/MoneyMovements/MoneyMovement.cs). Она относится к счёту, имеет дату, сумму, тип (доход или расход) и источник. Операции можно создать вручную, перенести между счетами, удалить и связать с чеком.

Связь операции с чеком хранится в [MoneyMovementReceipt](../CostVision.Domain/Models/MoneyMovements/MoneyMovementReceipt.cs). Сценарии [LinkMoneyMovementReceiptUseCase](../CostVision.Application/UseCases/MoneyMovements/ReceiptLinks/LinkMoneyMovementReceiptUseCase.cs) и [UnlinkMoneyMovementReceiptUseCase](../CostVision.Application/UseCases/MoneyMovements/ReceiptLinks/UnlinkMoneyMovementReceiptUseCase.cs) проверяют доступ и не позволяют создать некорректную связь.

Банковская выписка сначала разбирается в предпросмотре [PreviewBankStatementImportUseCase](../CostVision.Application/UseCases/MoneyMovements/BankStatementImports/PreviewBankStatementImportUseCase.cs). Он показывает ошибки распознавания и отмечает дубликаты — и в базе, и в самом файле. Затем [ImportMoneyMovementsUseCase](../CostVision.Application/UseCases/MoneyMovements/BankStatementImports/ImportMoneyMovementsUseCase.cs) сохраняет подтверждённые строки.

[AutoLinkExactMoneyMovementReceiptsUseCase](../CostVision.Application/UseCases/MoneyMovements/ReceiptLinks/AutoLinkExactMoneyMovementReceiptsUseCase.cs) автоматически связывает только однозначные пары: дата, сумма и счёт должны совпасть, при этом у каждого объекта должен быть единственный кандидат. Неоднозначные пары остаются пользователю.
