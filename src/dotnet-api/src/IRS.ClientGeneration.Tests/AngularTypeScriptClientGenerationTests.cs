using NSwag;
using NSwag.CodeGeneration.TypeScript;

namespace IRS.ClientGeneration.Tests;

/// <summary>
/// Ad-hoc client generation tests for generating Angular TypeScript client from Swagger/OpenAPI
/// 
/// IMPORTANT: These tests are NOT run by automated test runners.
/// Run manually only using: dotnet test --filter "FullyQualifiedName~ClientGeneration"
/// 
/// This follows the API policy requirement:
/// - Maintain ad-hoc client-generation tests
/// - Exclude from automated test runs
/// - Run manually only
/// </summary>
[TestFixture]
[Category("ClientGeneration")]
public class AngularTypeScriptClientGenerationTests
{
    private readonly string _outputDirectory = Path.Combine(
        Directory.GetCurrentDirectory(),
        "..",
        "..",
        "..",
        "..",
        "..",
        "..",
        "ClientApp",
        "src",
        "generated"
    );

    [SetUp]
    public void SetUp()
    {
        // Ensure output directory exists
        Directory.CreateDirectory(_outputDirectory);
    }


    [Test]
    [Explicit]
    public async Task GenerateAngularTypeScriptClient_WithInterceptors_CreatesProductionClient()
    {
        var swaggerUrl = "https://localhost:5001/swagger/v1/swagger.json";
        var clientOutputPath = Path.Combine(_outputDirectory, "generated");
        Directory.CreateDirectory(clientOutputPath);

        try
        {
            var document = await OpenApiDocument.FromUrlAsync(swaggerUrl);

            var settings = new TypeScriptClientGeneratorSettings
            {
                ClassName = "{controller}Client",
                Template = TypeScriptTemplate.Angular,
                GenerateClientClasses = true,
                GenerateClientInterfaces = true,
                InjectionTokenType = InjectionTokenType.InjectionToken,
                UseGetBaseUrlMethod = false,
                BaseUrlTokenName = "API_BASE_URL",
                UseTransformOptionsMethod = false,  // Set to false
                UseTransformResultMethod = false,   // Set to false
                WrapResponses = true,
                TypeScriptGeneratorSettings =
                {
                    TypeScriptVersion = 4.3m // or higher
                }
            };

            var generator = new TypeScriptClientGenerator(document, settings);
            var clientCode = generator.GenerateFile();

            var clientFilePath = Path.Combine(clientOutputPath, "irs-api.client.ts");
            await File.WriteAllTextAsync(clientFilePath, clientCode);

            Assert.That(File.Exists(clientFilePath), "Generated production client file should exist");
            Assert.That(clientCode, Does.Contain("Client"), "Client class should be generated");
            Assert.That(clientCode, Does.Contain("Observable"), "Should use RxJS Observables");

            TestContext.WriteLine($"✅ Generated production Angular client with interceptor support to: {clientFilePath}");
            TestContext.WriteLine($"📊 Client size: {clientCode.Length} characters");
            TestContext.WriteLine($"🔧 Features: HttpClient, RxJS, Request/Response Interceptors");
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"⚠️  Ensure IRS.Api is running on https://localhost:7000");
            TestContext.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }



}
