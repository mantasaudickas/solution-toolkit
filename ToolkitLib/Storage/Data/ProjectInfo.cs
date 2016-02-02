using System;
using System.Xml.Serialization;
using SolutionGenerator.Toolkit.Core;

namespace SolutionGenerator.Toolkit.Storage.Data
{
	[Serializable]
	public class ProjectInfo : DomainObject
	{
		[XmlAttribute("path")]
		public string ProjectFilePath { get; set; }
	}
}
