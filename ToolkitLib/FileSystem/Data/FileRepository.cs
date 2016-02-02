using System;
using SolutionGenerator.Toolkit.Core;

namespace SolutionGenerator.Toolkit.FileSystem.Data
{
	[Serializable]
	public class FileRepository : DomainObject
	{
		public FileInfoGroup[] Groups { get; set; }
	}
}
