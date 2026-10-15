using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using CostVision.Infrastructure.DataBase;
using EFCoreLibrary.Abstractions.Entity;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.SqlServer.Options;
using HttpClientLibrary.Abstractions;
using Xunit;

namespace CostVision.Infrastructure.UnitTests;

public class LibraryDeliveryManifestTests
{
    [Fact]
    public void ActualLoadedLibrariesAndXml_MatchAcceptedDeliveryManifest()
    {
        // Arrange
        string root = FindRepositoryRoot();
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "libs", "efcorelibrary-delivery-08a.json")));
        Assembly[] assemblies =
        [
            typeof(IEntity<Guid>).Assembly,
            typeof(IDatabaseMaintenance<CostVisionContextKey>).Assembly,
            typeof(SqlServerMaintenanceOptions).Assembly,
            typeof(IHttpApiClient).Assembly
        ];
        Dictionary<string, Assembly> loaded = assemblies.ToDictionary(
            assembly => Path.GetFileName(assembly.Location), StringComparer.Ordinal);

        // Act
        Dictionary<string, string> loadedHashes = loaded.ToDictionary(
            item => item.Key, item => CalculateHash(item.Value.Location), StringComparer.Ordinal);

        // Assert
        Assert.Equal("5BE84E7BE6E7111AAB7EBCCF7AC1609D012A6309634FCC7C8470973B04E19B58",
            manifest.RootElement.GetProperty("source").GetProperty("sourceSetSha256").GetString());
        Assert.Equal(6, manifest.RootElement.GetProperty("outputs").GetArrayLength());
        foreach (JsonElement output in manifest.RootElement.GetProperty("outputs").EnumerateArray())
        {
            string file = output.GetProperty("file").GetString()
                ?? throw new InvalidOperationException("В manifest отсутствует имя файла.");
            string expected = output.GetProperty("sha256").GetString()
                ?? throw new InvalidOperationException("В manifest отсутствует SHA-256.");
            Assert.Equal(expected, CalculateHash(Path.Combine(root, "libs", file)));
            if (file.EndsWith(".dll", StringComparison.Ordinal))
            {
                Assert.Equal(expected, loadedHashes[file]);
                Assert.Equal(output.GetProperty("assemblyVersion").GetString(), loaded[file].GetName().Version?.ToString());
            }
            else
            {
                string directory = Path.GetDirectoryName(loaded[Path.ChangeExtension(file, ".dll")].Location)
                    ?? throw new InvalidOperationException("Не найден каталог загруженной DLL.");
                Assert.Equal(expected, CalculateHash(Path.Combine(directory, file)));
            }
        }

        const string HTTP_SHA256 = "37B5EC623F8770CE66ADBCE0CECCB06F45368881AB07152AB5CFE94CC9C16925";
        Assert.Equal(HTTP_SHA256, loadedHashes["HttpClientLibrary.dll"]);
        Assert.Equal(HTTP_SHA256, CalculateHash(Path.Combine(root, "libs", "HttpClientLibrary.dll")));
    }

    [Fact]
    public void ActualRestoredInfrastructure_MatchesAcceptedConsumerPackageHashes()
    {
        // Arrange
        string root = FindRepositoryRoot();
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "libs", "efcorelibrary-delivery-08a.json")));
        using JsonDocument consumerPackages = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "libs", "costvision-consumer-packages.json")));
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "CostVision.Infrastructure", "obj", "project.assets.json")));
        JsonElement libraries = assets.RootElement.GetProperty("libraries");
        Dictionary<string, JsonElement> accepted = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement package in consumerPackages.RootElement.GetProperty("packages").EnumerateArray())
        {
            string identity = package.GetProperty("identity").GetString()
                ?? throw new InvalidOperationException("В manifest отсутствует package identity.");
            accepted.Add(identity[..identity.LastIndexOf('/')], package);
        }

        HashSet<string> relevantPackageNames = new(accepted.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement package in manifest.RootElement.GetProperty("libraryResolvedPackages").EnumerateArray())
        {
            string identity = package.GetProperty("identity").GetString()
                ?? throw new InvalidOperationException("В manifest отсутствует package identity.");
            relevantPackageNames.Add(identity[..identity.LastIndexOf('/')]);
        }

        // Act
        List<JsonProperty> actualPackages = libraries.EnumerateObject()
            .Where(package => relevantPackageNames.Contains(package.Name[..package.Name.LastIndexOf('/')])).ToList();

        // Assert
        Assert.NotEmpty(actualPackages);
        Assert.Equal(accepted.Count, actualPackages.Count);
        foreach (JsonProperty package in actualPackages)
        {
            string name = package.Name[..package.Name.LastIndexOf('/')];
            JsonElement expected = accepted[name];
            Assert.Equal(expected.GetProperty("identity").GetString(), package.Name);
            Assert.Equal(expected.GetProperty("nugetSha512").GetString(), package.Value.GetProperty("sha512").GetString());
        }

        foreach (JsonElement requirement in manifest.RootElement.GetProperty("runtimePackageRequirements").EnumerateArray())
        {
            string name = requirement.GetProperty("name").GetString()
                ?? throw new InvalidOperationException("В manifest отсутствует имя runtime package.");
            if (accepted.TryGetValue(name, out JsonElement package))
            {
                string identity = package.GetProperty("identity").GetString()
                    ?? throw new InvalidOperationException("В consumer manifest отсутствует package identity.");
                Assert.True(libraries.TryGetProperty(identity, out _), "Отсутствует обязательный runtime package: " + identity);
            }
            else
            {
                Assert.True(requirement.TryGetProperty("source", out _), "Отсутствует обязательный runtime package: " + name);
                string targetFramework = consumerPackages.RootElement.GetProperty("targetFramework").GetString()
                    ?? throw new InvalidOperationException("В consumer manifest отсутствует target framework.");
                Assert.True(assets.RootElement.GetProperty("project").GetProperty("frameworks").GetProperty(targetFramework)
                    .GetProperty("frameworkReferences").TryGetProperty("Microsoft.AspNetCore.App", out _));
            }
        }
    }

    private static string CalculateHash(string path)
    {
        using FileStream stream = File.OpenRead(path);

        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CostVision.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Не найден исходный checkout CostVision.");
    }
}
