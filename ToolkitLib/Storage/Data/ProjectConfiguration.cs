using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using SolutionGenerator.Toolkit.Core;

namespace SolutionGenerator.Toolkit.Storage.Data
{
	[Serializable]
	[XmlRoot("configuration")]
	public class ProjectConfiguration : DomainObject
	{
		[XmlIgnore]
		public string ResolvedConfigurationFilePath { get; set; }

		[XmlIgnore]
		public string ResolvedSourceRootPath { get; set; }

		[XmlElement("projectSetup")]
		public ProjectSetup ProjectSetup { get; set; }

		[XmlElement("appendToSolution")]
		public string CustomAppend { get; set; }

		[XmlArray("sources")]
		[XmlArrayItem("folder")]
		public string[] SourceFolders { get; set; }

		[XmlArray("thirdparty")]
		[XmlArrayItem("folder")]
		public string[] ThirdPartyFolders { get; set; }

		[XmlArray("groups")]
		[XmlArrayItem("group")]
		public ProjectGroup[] Groups { get; set; }

		public ProjectGroup Select(string groupName)
		{
			if (this.Groups == null)
				return null;

			return this.Groups.FirstOrDefault(projectGroup => String.Equals(projectGroup.Name, groupName, StringComparison.InvariantCultureIgnoreCase));
		}

		public string [] Select(string [] groupNames)
		{
			if (groupNames == null)
				return new string[0];

			HashSet<string> items = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			foreach (string groupName in groupNames)
			{
				ProjectGroup projectGroup = this.Select(groupName);
				if (projectGroup == null)
				{
					throw new SolutionGeneratorException("Group with name {0} not found in config file.", groupName);
				}

				items.UnionWith(projectGroup.Items);
			}

			return items.ToArray();
		}
	}
}
