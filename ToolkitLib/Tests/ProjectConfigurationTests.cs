using System;
using System.IO;
using SolutionGenerator.Toolkit.Solutions;
using SolutionGenerator.Toolkit.Storage.Data;

namespace SolutionGenerator.Toolkit.Tests
{
	public class ProjectConfigurationTests
	{
		public static int Test()
		{
			//TestRelativePathCreation();
			TestProjectConfigurationSerialization();
			//TestProjectConfigurationReadAndDeserialization();
			return 0;
		}

		private static void TestRelativePathCreation()
		{
			string solutionFilePath = @"c:\work\projects\strauss\sources\esg\buildscripts\solutions\interfaces.core.sln";
			string projectFilePath = @"c:\work\projects\strauss\sources\esg\shared\services\interfaces\core\esg.shared.services.interfaces.core.csproj";

			string relativePath = SolutionCreator.MakeRelativePath(solutionFilePath, projectFilePath);
			Console.WriteLine("Solution file  = {0}", solutionFilePath);
			Console.WriteLine("Project file   = {0}", projectFilePath);
			Console.WriteLine("Relative path  = {0}", relativePath);
		}

		private static void TestProjectConfigurationSerialization()
		{
			ProjectConfiguration configuration = new ProjectConfiguration();

			configuration.ProjectSetup = new ProjectSetup();
			configuration.ProjectSetup.RequiredProjectFiles = new [] { "BSS.Shared.v5\\SNK\\AssemblyKey.snk", "Source\\AssemblyInfoCommon.cs" };

			configuration.SourceFolders = new [] {"BSS.Shared.v6\\Source", "Source"};
			configuration.ThirdPartyFolders = new[] {"Binaries"};

			configuration.Groups = new ProjectGroup[3];

			ProjectGroup projectGroup = new ProjectGroup();
			projectGroup.Name = "Services";
			projectGroup.Items = new[]
			                        	{
			                        		@"Source\Application\ProcessingService\BSS.ERP.Application.ProcessingService-trade.csproj"
			                        	};
			configuration.Groups[0] = projectGroup;

			ProjectGroup secondProjectGroup = new ProjectGroup();
			secondProjectGroup.Name = "GUI";
			secondProjectGroup.Items = new[]
			                              	{
			                              		@"Source\GUI\Windows\TradeLauncher\BSS.ERP.GUI.Windows.Launcher-trade.csproj",
			                              	};
			configuration.Groups[1] = secondProjectGroup;


			ProjectGroup groupGroup = new ProjectGroup();
			groupGroup.ContainsGroupNames = true;
			groupGroup.Name = "All";
			groupGroup.Items = new [] {"Services", "GUI"};
			configuration.Groups[2] = groupGroup;

			Console.WriteLine(configuration.XmlSerialize());
		}

		private static void TestProjectConfigurationReadAndDeserialization()
		{
			FileInfo fileInfo = new FileInfo("Tests\\sampleconfiguration.xml");
			ProjectConfiguration configuration = fileInfo.XmlDeserialize<ProjectConfiguration>();
			DisplayConfiguration(configuration);
			DisplayConfiguration((ProjectConfiguration) configuration.Clone());
		}

		private static void DisplayConfiguration(ProjectConfiguration configuration)
		{
			if (configuration == null)
				throw new NullReferenceException("configuration is null");
			if (configuration.Groups == null)
				throw new NullReferenceException("configuration.Groups are null");

			foreach (ProjectGroup projectGroup in configuration.Groups)
			{
				Console.WriteLine("Group = {0}, Items = {1}", projectGroup.Name, projectGroup.Items != null ? projectGroup.Items.Length : 0);
				if (projectGroup.Items != null)
				{
					foreach (string projectInfo in projectGroup.Items)
					{
						Console.WriteLine("Project = {0}", projectInfo);
					}
				}
			}
		}
	}
}
