using Serilog.Core;
using Serilog.Events;

namespace CostVision.Services.Extensions
{
    /// <summary>
    /// Перехватывает каждое событие логирования и добавляет свойство "SourceContext" с простым именем класса.
    /// Например, если полное имя класса "CRMService.Service.Entity.IssueService", то в лог будет добавлено свойство "SourceContext" со значением "IssueService".
    /// </summary>
    public class SimpleClassNameEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? sourceContextValue)
                && sourceContextValue is ScalarValue scalar && scalar.Value is string fullName)
            {
                string simpleName = fullName.Split('.').Last();
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SourceContext", simpleName));
            }
        }
    }
}
