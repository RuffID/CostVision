# Ручной CI

`main.yml` запускается только через workflow_dispatch. Frontend отдельно устанавливает lockfile dependencies без lifecycle scripts, собирает strict TypeScript и запускает изолированный node-набор. Matrix unit собирает конкретные csproj и запускает только три UnitTests; IntegrationTests имеет отдельное compile-only задание.

SDK задаёт global.json, Node — .node-version, npm — packageManager и явная проверка версии. Restore в CI явный, с корневым NuGet.Config; .NET build/test используют --no-restore и SkipFrontendBuild=true. Deployment workflow удалён по просьбе пользователя; автоматически восстанавливать CD или менять triggers нельзя.

После обновления пакета xunit.v3 до4.0.2 global.json выбирает Microsoft.Testing.Platform. Изолированные тесты запускаются через dotnet test --project с --no-build/--no-restore, отчёт xUnit TRX — через --report-xunit-trx. Старый positional project/VSTest --logger trx и неподключённый --report-trx не использовать.

Успех CI не разрешает SQL/HTTP/browser/maintenance/deployment и не подтверждает SingleInitializer. Новые внешние контуры требуют отдельного разрешения и проверки их actual fixtures. Production конфигурацию не заменять тестовыми настройками ради readiness.
