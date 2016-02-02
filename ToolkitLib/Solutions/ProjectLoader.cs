using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using SolutionGenerator.Toolkit.Solutions.Data;
using SolutionGenerator.Toolkit.Solutions.Project;
using SolutionGenerator.Toolkit.Storage.Data;

namespace SolutionGenerator.Toolkit.Solutions
{
	public class ProjectLoader : BaseComponent
	{
		private static readonly IDictionary<string, ProjectLoader> projectLoaderCache = new Dictionary<string, ProjectLoader>(StringComparer.InvariantCultureIgnoreCase);
		
		private readonly IDictionary<string, VSProject> loadedProjects = new ConcurrentDictionary<string, VSProject>(StringComparer.InvariantCultureIgnoreCase);
		private readonly IDictionary<Guid, string> loadedProjectsById = new ConcurrentDictionary<Guid, string>();
		private readonly IDictionary<string, string> loadedProjectsByOutput = new ConcurrentDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
		private readonly IDictionary<string, string> loadedProjectsByAssemblyName = new ConcurrentDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

		private ProjectLoader(ILogger logger) : base(logger)
		{
		}

		public static ProjectLoader Create(ILogger logger, string sourceRootFolder)
		{
			ProjectLoader projectLoader;
			if (!projectLoaderCache.TryGetValue(sourceRootFolder, out projectLoader))
			{
				projectLoader = new ProjectLoader(logger);
				projectLoaderCache.Add(sourceRootFolder, projectLoader);
			}
			return projectLoader;
		}

		public VSProject GetProjectById(Guid projectId)
		{
			VSProject project = null;
			string projectFileLocation;
			if (loadedProjectsById.TryGetValue(projectId, out projectFileLocation))
			{
				project = loadedProjects[projectFileLocation];
			}
			return project;
		}

		public VSProject GetProjectByOutput(string outputLocation)
		{
			VSProject project = null;
			string projectFileLocation;
			if (loadedProjectsByOutput.TryGetValue(outputLocation, out projectFileLocation))
			{
				project = loadedProjects[projectFileLocation];
			}
			return project;
		}

		public VSProject GetProjectByAssemblyName(string assemblyName)
		{
			VSProject project = null;
			string projectFileLocation;
			if (loadedProjectsByAssemblyName.TryGetValue(assemblyName, out projectFileLocation))
			{
				project = loadedProjects[projectFileLocation];
			}
			return project;
		}

		public int PreloadProjects(ProjectSetup projectSetup, IEnumerable<string> projectLocations)
		{
			int count = 0;
			if (projectLocations != null)
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				foreach (string projectLocation in projectLocations)
				{
					try
					{
						LoadProject(projectSetup, projectLocation);
						++count;
					}
					catch (Exception e)
					{
						throw new SolutionGeneratorException("Unable to load project [{0}].", e, projectLocation);
					}
				}

				stopwatch.Stop();
				++count;
				Logger.Trace("Loaded {0} projects in {1} ms", count, stopwatch.ElapsedMilliseconds);
			}
			return count;
		}

		/// <summary>
		/// Loads the project.
		/// </summary>
		/// <param name="projectSetup">The project setup.</param>
		/// <param name="projectLocation">The project location.</param>
		/// <param name="projectLocations">The project locations.</param>
		/// <returns></returns>
		public VSProject LoadProject(ProjectSetup projectSetup, string projectLocation)
		{
			if (loadedProjects.ContainsKey(projectLocation))
				return loadedProjects[projectLocation];

			// Load project:
			IProjectReader reader = ProjectReaderFactory.CreateProjectReader(projectSetup.ProjectReaderType, projectLocation);
			VSProject project = reader.LoadProject();

			// Check for main properties:
			if (project.ProjectId == Guid.Empty)
				throw new ApplicationException(string.Format("Property ProjectGuid is empty."));

			if (String.IsNullOrEmpty(project.RootNamespace))
				throw new ApplicationException(string.Format("Property RootNamespace is empty."));

			//
			// Process references
			//
			string projectFileFolder = Path.GetDirectoryName(projectLocation) ?? ".";

			if (projectSetup.WhenAssemblyKeyFileNotFound != ProjectSetupBehavior.Valid)
			{
				string resolvedPath = ResolveFilePath(project.AssemblyOriginatorKeyFile, projectFileFolder);
				if (string.IsNullOrEmpty(resolvedPath))
				{
					if (projectSetup.WhenRequiredProjectLinkNotFound == ProjectSetupBehavior.Fail)
					{
						throw new SolutionGeneratorException("Project {0} does not have key file {1}!", projectLocation, projectSetup.AssemblyKeyFile);
					}
					if (projectSetup.WhenRequiredProjectLinkNotFound == ProjectSetupBehavior.Warn)
					{
						Logger.Warn("Project {0} does not have key file {1}!", projectLocation, projectSetup.AssemblyKeyFile);
					}
				}
				else if (!string.Equals(resolvedPath, projectSetup.AssemblyKeyFile, StringComparison.InvariantCultureIgnoreCase))
				{
					if (projectSetup.WhenRequiredProjectLinkNotFound == ProjectSetupBehavior.Fail)
					{
						throw new SolutionGeneratorException("Project {0} has key file {1} but in wrong location {2}!", projectLocation, projectSetup.AssemblyKeyFile, resolvedPath);
					}
					if (projectSetup.WhenRequiredProjectLinkNotFound == ProjectSetupBehavior.Warn)
					{
						Logger.Warn("Project {0} has key file {1} but in wrong location {2}!", projectLocation, projectSetup.AssemblyKeyFile, resolvedPath);
					}
				}
			}

			IList<VSProjectReference> fileReferences = reader.FileReferences;
			if (fileReferences != null && fileReferences.Count > 0)
			{
				if (projectSetup.WhenContainsFileReferences == ProjectSetupBehavior.Fail)
				{
					throw new SolutionGeneratorException("Project {0} contains {1} file references!", projectLocation, fileReferences.Count);
				}
				if (projectSetup.WhenContainsFileReferences == ProjectSetupBehavior.Warn)
				{
					Logger.Warn("Project {0} contains {1} file references!", projectLocation, fileReferences.Count);
				}
			}

			IList<VSProjectReference> projectReferences = reader.ProjectReferences;
			if (projectReferences != null && projectReferences.Count > 0)
			{
				if (projectSetup.WhenContainsProjectReferences == ProjectSetupBehavior.Fail)
				{
					throw new SolutionGeneratorException("Project {0} contains {1} project references!", projectLocation, projectReferences.Count);
				}
				if (projectSetup.WhenContainsProjectReferences == ProjectSetupBehavior.Warn)
				{
					Logger.Warn("Project {0} contains {1} project references!", projectLocation, projectReferences.Count);
				}
			}

			IList<VSProjectReference> silverlightProjectReferences = reader.SilverlightProjectReferences;
			if (silverlightProjectReferences != null && silverlightProjectReferences.Count > 0)
			{
//				if (projectSetup.WhenContainsProjectReferences == ProjectSetupBehavior.Fail)
//				{
//					throw new SolutionGeneratorException("Project {0} contains {1} project references!", projectLocation, silverlightProjectReferences.Count);
//				}
				if (projectSetup.WhenContainsProjectReferences == ProjectSetupBehavior.Warn)
				{
					Logger.Warn("Project {0} contains silverlight {1} project references!", projectLocation, silverlightProjectReferences.Count);
				}
			}

			if (projectSetup.RequiredProjectFiles != null && projectSetup.RequiredProjectFiles.Length > 0 && projectSetup.WhenRequiredProjectLinkNotFound != ProjectSetupBehavior.Valid)
			{
				IList<VSIncludedFile> includedFiles = reader.IncludedFiles;
				IDictionary<string, VSIncludedFile> fileMap = new Dictionary<string, VSIncludedFile>(StringComparer.InvariantCultureIgnoreCase);
				foreach (VSIncludedFile file in includedFiles)
				{
					if (!string.IsNullOrEmpty(file.Include))
					{
						string fileName = Path.GetFileName(file.Include);
						if (!string.IsNullOrEmpty(fileName))
							fileMap[fileName] = file;
					}
				}

				foreach (string projectLink in projectSetup.RequiredProjectFiles)
				{
					string fileName = Path.GetFileName(projectLink);

					if (string.IsNullOrEmpty(fileName))
						continue;

					string filePathToCheck = null;
					VSIncludedFile includedFile;
					if (fileMap.TryGetValue(fileName, out includedFile))
					{
						filePathToCheck = ResolveFilePath(includedFile.Include, projectFileFolder);
					}

					if (string.IsNullOrEmpty(filePathToCheck))
					{
						if (projectSetup.WhenRequiredProjectLinkNotFound == ProjectSetupBehavior.Fail)
						{
							throw new SolutionGeneratorException("Project {0} does not include required file {1}!", projectLocation, projectLink);
						}
						if (projectSetup.WhenRequiredProjectLinkNotFound == ProjectSetupBehavior.Warn)
						{
							Logger.Warn("Project {0} does not include required file {1}!", projectLocation, projectLink);
						}
					}
					else if (!string.Equals(projectLink, filePathToCheck, StringComparison.InvariantCultureIgnoreCase))
					{
						if (projectSetup.WhenRequiredProjectLinkNotFound == ProjectSetupBehavior.Fail)
						{
							throw new SolutionGeneratorException("Project {0} includes required file {1} but it in wrong location {2}!", projectLocation, projectLink, filePathToCheck);
						}
						if (projectSetup.WhenRequiredProjectLinkNotFound == ProjectSetupBehavior.Warn)
						{
							Logger.Warn("Project {0} includes required file {1} but it in wrong location {2}!", projectLocation, projectLink, filePathToCheck);
						}
					}
				}
			}

			foreach (VSProjectReference reference in project.References)
			{
				if (!reference.IsSilverlightReference)
				{
					reference.ResolvedInclude = ResolveInclude(reference.Include);
					reference.ResolvedHintPath = ResolveHintPath(reference.HintPath, reference.ResolvedInclude, projectFileFolder, reader.OutputFolder);
				}
				else
				{
					string pathByReference = ResolveSilverlightProjectPathByReference(projectFileFolder, reference.HintPath);
					VSProject vsProject = LoadProject(projectSetup, pathByReference);
					reference.ResolvedHintPath = vsProject.ResolvedOutput;
					reference.Include = vsProject.AssemblyName;
					reference.ResolvedInclude = vsProject.ResolvedOutput;
				}
			}

			loadedProjects.Add(projectLocation, project);

			if (loadedProjectsById.ContainsKey(project.ProjectId))
			{
				throw new SolutionGeneratorException("Duplicated project ID = {0}, {3}location = {1}, {3}location = {2}",
					project.ProjectId, project.ProjectFileLocation, loadedProjectsById[project.ProjectId], Environment.NewLine);
			}
			loadedProjectsById.Add(project.ProjectId, projectLocation);

			if (loadedProjectsByOutput.ContainsKey(project.ResolvedOutput))
			{
				throw new SolutionGeneratorException("Duplicated project output path = {0}, {3}location = {1}, {3}location = {2}",
					project.ResolvedOutput, project.ProjectFileLocation, loadedProjectsByOutput[project.ResolvedOutput], Environment.NewLine);
			}
			loadedProjectsByOutput.Add(project.ResolvedOutput, projectLocation);

		    if (loadedProjectsByAssemblyName.ContainsKey(project.AssemblyName))
		    {
                throw new SolutionGeneratorException("Duplicated project assembly name = {0}, {3}location = {1}, {3}location = {2}",
                    project.AssemblyName, project.ProjectFileLocation, loadedProjectsByAssemblyName[project.AssemblyName], Environment.NewLine);
            }
            loadedProjectsByAssemblyName.Add(project.AssemblyName, projectLocation);

            return project;
		}

		private static string ResolveSilverlightProjectPathByReference(string projectFileFolder, string referenceHintPath)
		{
			try
			{
				if (!Path.IsPathRooted(referenceHintPath))
					referenceHintPath = Path.GetFullPath(FileSystem.PathHelper.PathCombine(projectFileFolder, referenceHintPath));
			}
			catch (Exception exc)
			{
				throw new SolutionGeneratorException("Unable to resolve Silverlight reference!", exc);
			}
			return referenceHintPath;
		}
		
		private static string ResolveInclude(string evaluatedInclude)
		{
			if (!string.IsNullOrEmpty(evaluatedInclude))
			{
				int commaIndex = evaluatedInclude.IndexOf(',');
				if (commaIndex > 0)
					evaluatedInclude = evaluatedInclude.Substring(0, commaIndex);
			}
			return evaluatedInclude;
		}

		private static string ResolveHintPath(string hintPath, string include, string projectFileFolder, string outputFolder)
		{
			try
			{
				if (string.IsNullOrEmpty(hintPath))
					hintPath = outputFolder;

				if (!Path.IsPathRooted(hintPath))
					hintPath = Path.GetFullPath(FileSystem.PathHelper.PathCombine(projectFileFolder, hintPath));

				if (!hintPath.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) && !hintPath.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
					hintPath = FileSystem.PathHelper.PathCombine(hintPath, string.Format("{0}.dll", include));

				hintPath = hintPath.Replace("&amp;", "&");
			}
			catch (Exception e)
			{
				throw new SolutionGeneratorException("Unable to resolve hintPath = {0}{3}include = {1}{3}projectFileFolder = {2}{3}", e, hintPath, include, projectFileFolder, Environment.NewLine);
			}
			return hintPath;
		}
		
		private static string ResolveFilePath(string hintPath, string projectFileFolder)
		{
			if (string.IsNullOrEmpty(hintPath))
				return hintPath;

			try
			{
				if (!Path.IsPathRooted(hintPath))
					hintPath = Path.GetFullPath(FileSystem.PathHelper.PathCombine(projectFileFolder, hintPath));
			}
			catch (Exception e)
			{
				throw new SolutionGeneratorException("Unable to resolve file = {0}{2}projectFileFolder = {1}{2}", e, hintPath, projectFileFolder, Environment.NewLine);
			}
			return hintPath;
		}
	}
}
