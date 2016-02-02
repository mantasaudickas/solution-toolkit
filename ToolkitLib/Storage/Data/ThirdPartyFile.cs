using System;
using System.IO;
using System.Xml.Serialization;
using SolutionGenerator.Toolkit.Solutions;
using SolutionGenerator.Toolkit.Core;

namespace SolutionGenerator.Toolkit.Storage.Data
{
	public class ThirdPartyFile : DomainObject
	{
		public ThirdPartyFile()
		{
		}

		public ThirdPartyFile(string sourceRootFolder, string fileLocation)
		{
			string relativePath = SolutionCreator.MakeRelativePath(Path.Combine(sourceRootFolder, "file.txt"), fileLocation);
			FileLocation = @"$(RootDir)\" + relativePath;
		}

		[XmlAttribute("Include")]
		public string FileLocation { get; set; }
	}
}
