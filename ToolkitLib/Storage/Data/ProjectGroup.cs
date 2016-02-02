using System;
using System.Xml.Serialization;
using SolutionGenerator.Toolkit.Core;

namespace SolutionGenerator.Toolkit.Storage.Data
{
	[Serializable]
	public class ProjectGroup : DomainObject
	{
		[XmlAttribute("name")]
		public string Name { get; set; }

		[XmlAttribute("containsGroupNames")]
		public bool ContainsGroupNames { get; set; }

		[XmlIgnore]
		public bool ContainsGroupNamesSpecified
		{
			get { return ContainsGroupNames; }
			set { ContainsGroupNames = value; }
		}

		[XmlArray("projects")]
		[XmlArrayItem("project")]
		public string[] Items { get; set; }
	}
}
