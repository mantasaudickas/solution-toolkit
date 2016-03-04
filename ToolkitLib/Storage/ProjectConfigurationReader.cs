using System;
using System.Collections.Generic;
using System.IO;
using SolutionGenerator.Toolkit.FileSystem;
using SolutionGenerator.Toolkit.Logging;
using SolutionGenerator.Toolkit.Storage.Data;

namespace SolutionGenerator.Toolkit.Storage
{
	public class ProjectConfigurationReader : BaseComponent
	{
		private static readonly IDictionary<string, ProjectConfiguration> configurations =
			new Dictionary<string, ProjectConfiguration>(StringComparer.InvariantCultureIgnoreCase);

		private readonly FileSearcher fileSearcher;

		public ProjectConfigurationReader(ILogger logger) : base(logger)
		{
			this.fileSearcher = new FileSearcher(logger);
		}

		public static bool SkipGroupsWithInvalidProjects { get; set; }

		protected FileSearcher FileSearcher { get { return this.fileSearcher; } }

		internal static void ClearCache()
		{
			configurations.Clear();
		}

		public ProjectConfiguration Read(string filePath, string sourceRootDir)
		{
			string key = string.Format("{0}.{1}", filePath, sourceRootDir);

			ProjectConfiguration configuration;
			if (configurations.TryGetValue(key, out configuration))
				return configuration;

			string sourceRootFolder = FileSearcher.LocateFolder(sourceRootDir);
			string configFilePath = FileSearcher.LocateFile(filePath, sourceRootFolder);

			Logger.Info("Using configuration file: {0}", configFilePath);
			Logger.Info("Source root folder is: {0}", sourceRootFolder);

			configuration = new FileInfo(configFilePath).XmlDeserialize<ProjectConfiguration>();

			if (configuration == null)
				throw new SolutionGeneratorException("Unable to read configuration from file [{0}]. Deserialized to NULL.", filePath);

			configuration.SourceFolders = FileSearcher.LocateFolders(configuration.SourceFolders, sourceRootFolder);
			configuration.ThirdPartyFolders = FileSearcher.LocateFolders(configuration.ThirdPartyFolders, sourceRootFolder);

			if (configuration.Groups != null)
			{
				HashSet<string> groupNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
				List<ProjectGroup> validGroups = new List<ProjectGroup>();
				foreach (ProjectGroup projectGroup in configuration.Groups)
				{
					if (groupNames.Contains(projectGroup.Name))
						throw new SolutionGeneratorException("Configuration invalid! Duplicated group names found! Duplicated name = {0}", projectGroup.Name);

					groupNames.Add(projectGroup.Name);

					if (projectGroup.ContainsGroupNames)
					{
						projectGroup.Items = CollectProjectsFromGroup(projectGroup.Name, configuration).ToArray();
						projectGroup.ContainsGroupNames = false;
					}

					try
					{
						projectGroup.Items = FileSearcher.LocateFiles(projectGroup.Items, sourceRootFolder);
						validGroups.Add(projectGroup);
					}
					catch
					{
						if (!SkipGroupsWithInvalidProjects)
							throw;
					}
				}

				configuration.Groups = validGroups.ToArray();
			}

			configuration.ResolvedSourceRootPath = sourceRootFolder;
			configuration.ResolvedConfigurationFilePath = configFilePath;

			if (configuration.ProjectSetup == null)
				configuration.ProjectSetup = new ProjectSetup();

			if (configuration.ProjectSetup.RequiredProjectFiles == null)
				configuration.ProjectSetup.RequiredProjectFiles = new string[0];

			configuration.ProjectSetup.RequiredProjectFiles = FileSearcher.LocateFiles(configuration.ProjectSetup.RequiredProjectFiles, sourceRootFolder);
			configuration.ProjectSetup.AssemblyKeyFile = FileSearcher.LocateFile(configuration.ProjectSetup.AssemblyKeyFile, sourceRootFolder);

			configurations.Add(key, configuration);

			return configuration;
		}

		private static List<string> CollectProjectsFromGroup(string currentGroupName, ProjectConfiguration configuration)
		{
			List<string> projectList = new List<string>();

			ProjectGroup projectGroup = configuration.Select(currentGroupName);

			if (projectGroup == null)
				throw new Exception(string.Format("Project group name {0} not found in configuration.", currentGroupName));

			if (projectGroup.ContainsGroupNames)
			{
				if (projectGroup.Items != null)
				{
					foreach (string groupName in projectGroup.Items)
					{
						IEnumerable<string> projects = CollectProjectsFromGroup(groupName, configuration);
						projectList.AddRange(projects);
					}
				}
			}
			else
			{
				projectList.AddRange(projectGroup.Items);
			}

			if (projectList.Count > 1)
			{
				// remove dublicates
				HashSet<string> duplicates = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
				for (int i = 0; i < projectList.Count; ++i)
				{
					string location = projectList[i];
					if (duplicates.Contains(location))
					{
						projectList.RemoveAt(i);
						--i;
					}
					duplicates.Add(location);
				}
			}
			return projectList;
		}

	}
}
