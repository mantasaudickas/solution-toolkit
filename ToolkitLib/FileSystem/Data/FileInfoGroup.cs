using System;
using System.Xml.Serialization;
using SolutionGenerator.Toolkit.Core;

namespace SolutionGenerator.Toolkit.FileSystem.Data
{
	[Serializable]
	public class FileInfoGroup : DomainObject
	{
		[XmlAttribute("folder")]
		public string Folder { get; set; }

		[XmlArrayItem("file")]
		public string[] Files { get; set; }
	}
}
