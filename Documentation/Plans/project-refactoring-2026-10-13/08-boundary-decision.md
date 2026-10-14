# Решение о границе хранения для этапа 08

Дата подготовки и решения: 14.10.2026. Статус: **Пользователь принял вариант А**.

## Проверенная исходная граница

Ниже сохранено основание решения до 08A (DLL 0.0.2/EF 10.0.10). Актуализация 08A: подключены base 0.0.5 и Maintenance/SqlServer 1.0.0, EF 10.0.12; Application наследует context-free DLL CRUD/predicate-интерфейсы, но AppDbContextBase/EF/include/tracking/projection остаются. Infrastructure использует IContext*Repository и один CostVisionContextKey, технический scope — IUnitOfWorkContext, maintenance — библиотечный coordinator той же scoped-сессии. Это согласованная адаптация API в рамках А, а не реализация Б. [Поставка и actual изолированная проверка](../../../libs/README.md); SQL/production-приёмка остаётся открытой.

Domain ссылается на EFCoreLibrary.dll ради IEntity; Application явно ссылается на EF Core/Relational 10.0.10 и эту DLL. AppDbContextBase наследует DbContext. Одиннадцать предметных repository-интерфейсов наследуют базовые DLL-контракты с AppDbContextBase. Application задаёт predicate, include, tracking и общую projection-композицию. Infrastructure исполняет её принятой DLL; SQL Server provider находится только там.

Фактические версии и доступные контракты поставки фиксируются в [libs/README.md](../../../libs/README.md). Глобальный UoW уже удалён в 07A; повторное разделение или второй контекст не нужны. Владельцы сохранения указаны в [карте 07A](07a-consumer-map.md).

## Вариант А: сохранить явную EF-границу в 08

Принят для текущего этапа: сохранить существующие AppDbContextBase и DLL-наследование репозиториев, EF/IQueryable/predicate/include и согласованный GetProjectedItemsAsync<TResult>. Механический фасад страницы удалён, проверка файла перенесена в preview-сценарий; библиотечная/транзакционная граница описана. Зависимость Application от EF считается осознанным ограничением, независимость Domain от всей DLL не заявляется.

Это не удаление EF и не выполнение общего требования backend-скилла о чистых Application-портах. Правило проекта о композиции queries в Application сохраняется. При согласовании варианта А критерий 08 проверяет эту фактическую границу; SQL/provider/HTTP/browser-приёмка остаётся открытой.

## Вариант Б: убрать context/DLL из Application, сохранить query-контракт

Совместимый промежуточный вариант, также требующий отдельного решения до переноса:

- Application объявляет собственные общие CRUD-интерфейсы без TContext. Сохраняются имена Create/CreateRange/Delete/DeleteRange/GetItemByIdAsync/GetItemByPredicateAsync/GetItemsByPredicateAsync, параметры/defaults, nullable-результаты и CancellationToken.
- GetItemByPredicateAsync принимает Expression<Func<TEntity, bool>>, bool asNoTracking=false, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include=null и ct. List дополнительно сохраняет predicate=null, skip=0 и take=null. GetProjectedItemsAsync<TResult>(predicate, query, ct) не меняется.
- AppDbContextBase переносится в Infrastructure; реализации предметных репозиториев адаптируют собственные контракты к actual DLL CRUD/query API. Исполнение Query(asNoTracking:true) + Where(predicate) + query + ToListAsync(ct) остаётся прежним.
- Domain продолжает использовать actual IEntity; это отдельно оставшаяся ссылка на DLL. Application остаётся зависимым от EF из-за Include/ThenInclude/AsSplitQuery. Вариант Б не называется полным удалением EF.
- Один ApplicationContext, два библиотечных context adapters и один UnitOfWorkScope сохраняются. Ни repository, ни PageModel не получают права открывать дополнительную транзакцию или сохранять вместо её владельца.

Этот вариант не принят и не реализован. При отдельном разрешении новые интерфейсы описывали бы только совместимую поверхность, DLL исполняла бы операции без копирования persistence. Проверки должны покрыть defaults/nullable/token, actual DI/tracker/projection и оба transaction overloads, а также регрессии 01–07A. Перенос по одной группе обязан оставлять проекты компилируемыми.

## Полное удаление EF/IQueryable/include

Это отдельное несовместимое изменение утверждённого контракта 07. Одно переименование интерфейсов его не решает. Нужен согласованный общий язык графа загрузки (типизированные navigation paths, split/tracking), проекций, групп, порядка и страниц, сохраняющий переводимость predicates и все результаты 01–07. Принятая DLL предоставляет IQueryable/include, но не такой независимый язык: adapter проекта пришлось бы проектировать и проверять отдельно. Специализированные Get...With/GetAccessible-методы противоречат правилам проекта и не предлагаются как обход.

Без отдельного решения эта замена не реализуется. Ни reflection в production, ни custom SQL, replacement runner, fallback, второй контекст или перенос финансовых правил в Infrastructure не являются допустимым вариантом.

## Итог решения

Пользователь явно выбрал «А: сохранить текущую явную EF/DLL-границу в 08». Б и полное удаление EF не приняты и не начаты. Stage08BoundaryTests закрепляет actual контекст, наследование DLL CRUD и generic projection; Stage07ReadQueryTests сохраняет defaults/include DLL. Новая независимость от EF не заявляется. Решение и изолированные проверки не закрывают SQL-приёмку 01–07A.
