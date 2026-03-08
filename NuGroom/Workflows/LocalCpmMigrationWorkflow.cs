using NuGroom.Configuration;
using NuGroom.Nuget;

namespace NuGroom.Workflows
{
	/// <summary>
	/// Executes Central Package Management migration directly on local files.
	/// </summary>
	internal static class LocalCpmMigrationWorkflow
	{
		/// <summary>
		/// Generates and applies CPM migration changes for local scan mode.
		/// </summary>
		/// <param name="parseResult">Parse result containing local paths and CPM options.</param>
		/// <param name="references">Scanned package references.</param>
		public static void Execute(ParseResult parseResult, List<PackageReferenceExtractor.PackageReference> references)
		{
			ArgumentNullException.ThrowIfNull(parseResult);
			ArgumentNullException.ThrowIfNull(references);

			var localRoots = ResolveLocalRoots(parseResult.LocalPaths);

			if (localRoots.Count == 0)
			{
				return;
			}

			var effectiveUpdateConfig = UpdateConfig.GetEffective(parseResult.UpdateConfig);

			var isDryRun = effectiveUpdateConfig.DryRun;
			var eligibleReferences = references
				.Where(r => r.SourceKind == PackageSourceKind.ProjectFile)
				.Where(r => !string.IsNullOrWhiteSpace(r.Version))
				.ToList();

			if (eligibleReferences.Count == 0)
			{
				ConsoleWriter.Out.Yellow().WriteLine("No eligible package references found for local CPM migration.").ResetColor();

				return;
			}

			foreach (var root in localRoots)
			{
				MigrateRoot(root, eligibleReferences, parseResult.PerProject, isDryRun);
			}
		}

		private static List<string> ResolveLocalRoots(List<string>? localPaths)
		{
			var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			if (localPaths == null)
			{
				return roots.ToList();
			}

			foreach (var rawPath in localPaths)
			{
				var expandedPath = Environment.ExpandEnvironmentVariables(rawPath);
				var fullPath = Path.GetFullPath(expandedPath);

				if (Directory.Exists(fullPath))
				{
					roots.Add(fullPath);
					continue;
				}

				if (!File.Exists(fullPath))
				{
					continue;
				}

				var parentDirectory = Path.GetDirectoryName(fullPath);

				if (!string.IsNullOrWhiteSpace(parentDirectory))
				{
					roots.Add(parentDirectory);
				}
			}

			return roots.ToList();
		}

		private static void MigrateRoot(
			string root,
			List<PackageReferenceExtractor.PackageReference> eligibleReferences,
			bool perProject,
			bool isDryRun)
		{
			var referencesForRoot = eligibleReferences
				.Where(r => IsPathWithinRoot(root, r.ProjectPath))
				.ToList();

			if (referencesForRoot.Count == 0)
			{
				return;
			}

			var migrationInput = BuildMigrationInput(root, referencesForRoot, perProject);
			var migrationResult = CpmMigrationGenerator.Migrate(
				migrationInput.References,
				migrationInput.ProjectContents,
				perProject,
				migrationInput.ExistingPropsContents);

			if (migrationResult.FileChanges.Count == 0)
			{
				ConsoleWriter.Out.WriteLine($"No CPM changes generated for root: {root}");
				return;
			}

			PrintMigrationSummary(root, migrationResult, isDryRun);

			if (isDryRun)
			{
				return;
			}

			ApplyFileChanges(root, migrationResult.FileChanges);
		}

		private static LocalMigrationInput BuildMigrationInput(
			string root,
			List<PackageReferenceExtractor.PackageReference> referencesForRoot,
			bool perProject)
		{
			var normalizedReferences = new List<PackageReferenceExtractor.PackageReference>();
			var projectContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var existingPropsContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			foreach (var reference in referencesForRoot)
			{
				var relativeProjectPath = NormalizePath(Path.GetRelativePath(root, reference.ProjectPath));
				normalizedReferences.Add(reference with { ProjectPath = relativeProjectPath });

				if (!projectContents.ContainsKey(relativeProjectPath) && File.Exists(reference.ProjectPath))
				{
					projectContents[relativeProjectPath] = File.ReadAllText(reference.ProjectPath);
				}
			}

			if (perProject)
			{
				foreach (var relativeProjectPath in projectContents.Keys)
				{
					var propsRelativePath = NormalizePath(Path.Combine(Path.GetDirectoryName(relativeProjectPath) ?? string.Empty, "Directory.Packages.props"));
					var propsFullPath = Path.Combine(root, propsRelativePath.Replace('/', Path.DirectorySeparatorChar));

					if (File.Exists(propsFullPath))
					{
						existingPropsContents[propsRelativePath] = File.ReadAllText(propsFullPath);
					}
				}
			}
			else
			{
				var rootPropsPath = Path.Combine(root, "Directory.Packages.props");

				if (File.Exists(rootPropsPath))
				{
					existingPropsContents["Directory.Packages.props"] = File.ReadAllText(rootPropsPath);
				}
			}

			return new LocalMigrationInput(normalizedReferences, projectContents, existingPropsContents);
		}

		private static bool IsPathWithinRoot(string root, string filePath)
		{
			var normalizedRoot = Path.GetFullPath(root)
				.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				+ Path.DirectorySeparatorChar;
			var normalizedFile = Path.GetFullPath(filePath);

			return normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
		}

		private static string NormalizePath(string path)
		{
			return path.Replace('\\', '/');
		}

		private static void PrintMigrationSummary(string root, CpmMigrationResult migrationResult, bool isDryRun)
		{
			var mode = isDryRun ? "dry-run" : "apply";
			var newFiles = migrationResult.FileChanges.Count(f => f.IsNew);
			var modifiedFiles = migrationResult.FileChanges.Count(f => !f.IsNew);

			ConsoleWriter.Out.WriteLine();
			ConsoleWriter.Out.WriteLine(new string('=', 80));
			ConsoleWriter.Out.WriteLine($"LOCAL CPM MIGRATION ({mode}): {root}");
			ConsoleWriter.Out.WriteLine(new string('=', 80));
			ConsoleWriter.Out.WriteLine($"Planned changes: {newFiles} new file(s), {modifiedFiles} modified file(s), {migrationResult.Conflicts.Count} conflict(s)");

			if (!isDryRun)
			{
				return;
			}

			foreach (var fileChange in migrationResult.FileChanges)
			{
				var action = fileChange.IsNew ? "create" : "modify";
				ConsoleWriter.Out.WriteLine($"  Would {action}: {fileChange.FilePath}");
			}
		}

		private static void ApplyFileChanges(string root, List<CpmFileChange> fileChanges)
		{
			foreach (var fileChange in fileChanges)
			{
				var fullPath = Path.Combine(root, fileChange.FilePath.Replace('/', Path.DirectorySeparatorChar));
				var directory = Path.GetDirectoryName(fullPath);

				if (!string.IsNullOrWhiteSpace(directory))
				{
					Directory.CreateDirectory(directory);
				}

				File.WriteAllText(fullPath, fileChange.Content);
				var action = fileChange.IsNew ? "Created" : "Updated";
				ConsoleWriter.Out.Green().WriteLine($"  {action}: {fullPath}").ResetColor();
			}
		}

		private sealed record LocalMigrationInput(
			List<PackageReferenceExtractor.PackageReference> References,
			Dictionary<string, string> ProjectContents,
			Dictionary<string, string> ExistingPropsContents);
	}
}
