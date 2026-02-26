using NuGroom.Nuget;
using NuGroom.Reporting;

using Shouldly;

using System.Text.Json;

namespace NuGroom.Tests
{
	[TestFixture]
	public class SpdxSbomExporterTests
	{
		[Test]
		public void WhenReferencesProvidedThenSbomFileIsCreated()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.3",
					ProjectPath: "src/MyApp/MyApp.csproj",
					RepositoryName: "MyApp",
					ProjectName: "MyApp",
					LineNumber: 10)
			};

			var path = Path.Combine(Path.GetTempPath(), $"sbom-{Guid.NewGuid()}.spdx.json");

			SpdxSbomExporter.Export(references, path);

			File.Exists(path).ShouldBeTrue();
		}

		[Test]
		public void WhenSbomExportedThenContainsSpdxContext()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Serilog",
					Version: "3.1.1",
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "App",
					ProjectName: "App",
					LineNumber: 5)
			};

			var path = Path.Combine(Path.GetTempPath(), $"sbom-{Guid.NewGuid()}.spdx.json");

			SpdxSbomExporter.Export(references, path);

			var json = File.ReadAllText(path);
			var doc = JsonDocument.Parse(json);

			doc.RootElement.GetProperty("@context").GetString()
				.ShouldBe("https://spdx.org/rdf/3.0.0/spdx-context.jsonld");
		}

		[Test]
		public void WhenSbomExportedThenContainsSpdxDocumentElement()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Moq",
					Version: "4.20.72",
					ProjectPath: "src/Tests/Tests.csproj",
					RepositoryName: "Tests",
					ProjectName: "Tests",
					LineNumber: 8)
			};

			var path = Path.Combine(Path.GetTempPath(), $"sbom-{Guid.NewGuid()}.spdx.json");

			SpdxSbomExporter.Export(references, path);

			var json = File.ReadAllText(path);
			var doc = JsonDocument.Parse(json);
			var graph = doc.RootElement.GetProperty("@graph");

			graph.GetArrayLength().ShouldBeGreaterThan(0);
			graph[0].GetProperty("type").GetString().ShouldBe("SpdxDocument");
		}

		[Test]
		public void WhenSbomExportedThenContainsPackageElement()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "NUnit",
					Version: "4.3.2",
					ProjectPath: "src/Tests/Tests.csproj",
					RepositoryName: "Tests",
					ProjectName: "Tests",
					LineNumber: 12)
			};

			var path = Path.Combine(Path.GetTempPath(), $"sbom-{Guid.NewGuid()}.spdx.json");

			SpdxSbomExporter.Export(references, path);

			var json = File.ReadAllText(path);
			var doc = JsonDocument.Parse(json);
			var graph = doc.RootElement.GetProperty("@graph");

			// First element is SpdxDocument, second is the package
			graph.GetArrayLength().ShouldBe(2);
			var pkg = graph[1];
			pkg.GetProperty("type").GetString().ShouldBe("software_Package");
			pkg.GetProperty("name").GetString().ShouldBe("NUnit");
			pkg.GetProperty("packageVersion").GetString().ShouldBe("4.3.2");
		}

		[Test]
		public void WhenPackageHasVersionThenExternalIdentifierContainsPurl()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "xunit",
					Version: "2.9.0",
					ProjectPath: "src/Tests/Tests.csproj",
					RepositoryName: "Tests",
					ProjectName: "Tests",
					LineNumber: 15)
			};

			var path = Path.Combine(Path.GetTempPath(), $"sbom-{Guid.NewGuid()}.spdx.json");

			SpdxSbomExporter.Export(references, path);

			var json = File.ReadAllText(path);
			var doc = JsonDocument.Parse(json);
			var pkg = doc.RootElement.GetProperty("@graph")[1];
			var extId = pkg.GetProperty("externalIdentifier")[0];

			extId.GetProperty("externalIdentifierType").GetString().ShouldBe("purl");
			extId.GetProperty("identifier").GetString().ShouldBe("pkg:nuget/xunit@2.9.0");
		}

		[Test]
		public void WhenEmptyReferencesThenSbomContainsOnlyDocument()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>();

			var path = Path.Combine(Path.GetTempPath(), $"sbom-{Guid.NewGuid()}.spdx.json");

			SpdxSbomExporter.Export(references, path);

			var json = File.ReadAllText(path);
			var doc = JsonDocument.Parse(json);
			var graph = doc.RootElement.GetProperty("@graph");

			graph.GetArrayLength().ShouldBe(1);
			graph[0].GetProperty("type").GetString().ShouldBe("SpdxDocument");
		}

		[Test]
		public void WhenPathIsNullThenThrowsArgumentException()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>();

			Should.Throw<ArgumentException>(() => SpdxSbomExporter.Export(references, null!));
		}

		[Test]
		public void WhenReferencesIsNullThenThrowsArgumentNullException()
		{
			Should.Throw<ArgumentNullException>(() => SpdxSbomExporter.Export(null!, "test.json"));
		}

		[Test]
		public void WhenMultipleReferencesThenAllPackagesIncluded()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "PackageA",
					Version: "1.0.0",
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "App",
					ProjectName: "App",
					LineNumber: 1),
				new(
					PackageName: "PackageB",
					Version: "2.0.0",
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "App",
					ProjectName: "App",
					LineNumber: 2),
				new(
					PackageName: "PackageC",
					Version: "3.0.0",
					ProjectPath: "src/Lib/Lib.csproj",
					RepositoryName: "Lib",
					ProjectName: "Lib",
					LineNumber: 3)
			};

			var path = Path.Combine(Path.GetTempPath(), $"sbom-{Guid.NewGuid()}.spdx.json");

			SpdxSbomExporter.Export(references, path);

			var json = File.ReadAllText(path);
			var doc = JsonDocument.Parse(json);
			var graph = doc.RootElement.GetProperty("@graph");

			// 1 SpdxDocument + 3 packages
			graph.GetArrayLength().ShouldBe(4);
		}
	}
}
