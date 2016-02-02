using System;
using System.Xml.Serialization;
using SolutionGenerator.Toolkit.Core;

namespace SolutionGenerator.Toolkit.Storage.Data
{
	[XmlRoot("Project", Namespace = "http://schemas.microsoft.com/developer/msbuild/2003")]
	public class ThirdPartyFileContainer : DomainObject
	{
		[XmlArray("ItemGroup")]
		[XmlArrayItem("ThirdPartyFiles")]
		public ThirdPartyFile[] Items { get; set; }
	}
}
