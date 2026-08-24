using System.Runtime.InteropServices;
using CostVision.Infrastructure.DataBase;

namespace CostVision.Infrastructure.IntegrationTests.Fixtures;

internal static class DatabaseContainerSettings
{
    public const string SQL_IMAGE_ENVIRONMENT_VARIABLE_NAME = "COSTVISION_TEST_SQL_IMAGE";
    public const string MSSQL_SERVER_IMAGE = "mcr.microsoft.com/mssql/server:2022-latest";
    public const string AZURE_SQL_EDGE_IMAGE = "mcr.microsoft.com/azure-sql-edge:latest";
    public const ushort SQL_SERVER_PORT = 1433;

    public static string DatabaseName
    {
        get
        {
            string assemblyName = typeof(ApplicationContext).Assembly.GetName().Name
                ?? nameof(ApplicationContext);
            string projectName = assemblyName.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];

            return $"{projectName}_IntegrationTests";
        }
    }

    public static string ResolveImage()
    {
        string? configuredImage = Environment.GetEnvironmentVariable(SQL_IMAGE_ENVIRONMENT_VARIABLE_NAME);

        if (!string.IsNullOrWhiteSpace(configuredImage))
            return configuredImage;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            return AZURE_SQL_EDGE_IMAGE;

        return MSSQL_SERVER_IMAGE;
    }
}
