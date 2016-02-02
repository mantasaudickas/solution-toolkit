﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SolutionGenerator.Toolkit.Solutions.Data;
using SolutionGenerator.Toolkit.Storage.Data;

namespace SolutionGenerator.Toolkit.Solutions
{
	public class SolutionCreator : BaseComponent
	{
		private readonly ReferenceWalker referenceWalker;

		public SolutionCreator(ILogger logger) : base(logger)
		{
			this.referenceWalker = new ReferenceWalker(logger);
		}

		protected ReferenceWalker ReferenceWalker { get { return this.referenceWalker; } }

		public GeneratedSolution CreateSolution(ProjectSetup projectSetup, ProjectLoader projectLoader, IEnumerable<string> projectLocations, string solutionFileLocation, string[] thirdPartyFolders, HashSet<string> usedThirdParties, string customAppend)
		{
			List<Guid> solutionProjectList = ReferenceWalker.WalkReferencesRecursively(projectSetup, projectLoader, projectLocations, thirdPartyFolders, usedThirdParties);

			StringBuilder solutionFile = new StringBuilder();
			solutionFile.AppendLine();
			solutionFile.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
			solutionFile.AppendLine("# Visual Studio 2014");

			if (!string.IsNullOrWhiteSpace(customAppend))
			{
				solutionFile.AppendLine(customAppend);
			}

			foreach (Guid projectId in solutionProjectList)
			{
				VSProject project = projectLoader.GetProjectById(projectId);

				// FullProjectPath can be changed to relative, but then root folder is needed
				string projectFileLocation = MakeRelativePath(solutionFileLocation, project.ProjectFileLocation);

				solutionFile.AppendFormat("Project(\"{0}\") = \"{1}\", \"{2}\", \"{3}\"",
				                          "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}",
				                          Path.GetFileNameWithoutExtension(projectFileLocation),
				                          projectFileLocation,
				                          project.ProjectId.ToString("B"));
				solutionFile.AppendLine();

				#region DEPENDENCIES

				List<Guid> dependencies = ReferenceWalker.WalkReferencesRecursively(projectSetup, projectLoader, project, thirdPartyFolders, usedThirdParties);

				if (dependencies != null && dependencies.Count > 0)
				{
					bool projectSectionAdded = false;
					foreach (Guid dependentItemId in dependencies)
					{
						// do not add dependency to itself
						if (dependentItemId == project.ProjectId)
							continue;

						if (!projectSectionAdded)
						{
							solutionFile.AppendLine("\tProjectSection(ProjectDependencies) = postProject");
							projectSectionAdded = true;
						}

						solutionFile.AppendFormat("\t\t{0} = {0}", dependentItemId.ToString("B"));
						solutionFile.AppendLine();
					}

					if (projectSectionAdded)
						solutionFile.AppendLine("\tEndProjectSection");
				}

				#endregion

				solutionFile.AppendLine("EndProject");
			}

			solutionFile.AppendLine("Global");

			#region GLOBAL SECTION

			solutionFile.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
			solutionFile.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
			solutionFile.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");

			solutionFile.AppendLine("\t\tDebug|Mixed Platforms = Debug|Mixed Platforms");
			solutionFile.AppendLine("\t\tRelease|Mixed Platforms = Release|Mixed Platforms");

			solutionFile.AppendLine("\t\tDebug|X64 = Debug|X64");
			solutionFile.AppendLine("\t\tRelease|X64 = Release|X64");

			solutionFile.AppendLine("\t\tDebug|x86 = Debug|x86");
			solutionFile.AppendLine("\t\tRelease|x86 = Release|x86");

			solutionFile.AppendLine("\tEndGlobalSection");

			#endregion

			#region GLOBAL SECTION

			solutionFile.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
			foreach (Guid projectId in solutionProjectList)
			{
				string idString = projectId.ToString("B");
				solutionFile.AppendFormat("\t\t{0}.Debug|Any CPU.ActiveCfg = Debug|Any CPU", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Debug|Any CPU.Build.0 = Debug|Any CPU", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Debug|Mixed Platforms.ActiveCfg = Debug|Any CPU", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Debug|Mixed Platforms.Build.0 = Debug|Any CPU", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Debug|X64.ActiveCfg = Debug|X64", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Debug|X64.Build.0 = Debug|X64", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Debug|x86.ActiveCfg = Debug|x86", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Debug|x86.Build.0 = Debug|x86", idString).AppendLine();

				solutionFile.AppendFormat("\t\t{0}.Release|Any CPU.ActiveCfg = Release|Any CPU", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Release|Any CPU.Build.0 = Release|Any CPU", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Release|Mixed Platforms.ActiveCfg = Release|Any CPU", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Release|Mixed Platforms.Build.0 = Release|Any CPU", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Release|X64.ActiveCfg = Release|X64", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Release|X64.Build.0 = Release|X64", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Release|x86.ActiveCfg = Release|x86", idString).AppendLine();
				solutionFile.AppendFormat("\t\t{0}.Release|x86.Build.0 = Release|x86", idString).AppendLine();
			}
			solutionFile.AppendLine("\tEndGlobalSection");

			#endregion

			solutionFile.AppendLine("EndGlobal");

			Logger.Trace("Created solution file with {0} projects", solutionProjectList.Count);

			GeneratedSolution generatedSolution = new GeneratedSolution();
			generatedSolution.Content = solutionFile.ToString();
			generatedSolution.IncludedProjects = solutionProjectList.Count;
			return generatedSolution;
		}

		public static string MakeRelativePath(string solutionFileLocation, string projectFileLocation)
		{
			if (!Path.IsPathRooted(solutionFileLocation))
				return projectFileLocation;

			if (!Path.IsPathRooted(projectFileLocation))
				return projectFileLocation;

			string projectFileName = Path.GetFileName(projectFileLocation);
			projectFileLocation = Path.GetDirectoryName(projectFileLocation) ?? ".";
			solutionFileLocation = Path.GetDirectoryName(solutionFileLocation) ?? ".";

			string[] solutionPathNodes = solutionFileLocation.Split("\\".ToCharArray());
			string[] projectPathNodes = projectFileLocation.Split("\\".ToCharArray());

			if (solutionPathNodes.Length == 0 || projectPathNodes.Length == 0)
				return projectFileLocation;

			if (!string.Equals(solutionPathNodes[0], projectPathNodes[0], StringComparison.InvariantCultureIgnoreCase))
				return projectFileLocation;

			int length = Math.Min(solutionPathNodes.Length, projectPathNodes.Length);
			int startIndex = 1;
			for (int i = 1; i < length; ++i)
			{
				if (!string.Equals(solutionPathNodes[i], projectPathNodes[i], StringComparison.InvariantCultureIgnoreCase))
				{
					break;
				}
				++startIndex;
			}

			string relativePath = string.Empty;
			for (int i = startIndex; i < solutionPathNodes.Length; ++i)
			{
				relativePath += "..\\";
			}

			for (int i = startIndex; i < projectPathNodes.Length; ++i)
			{
				relativePath += projectPathNodes[i] + "\\";
			}

			relativePath += projectFileName;

			return relativePath;
		}
	}
}
