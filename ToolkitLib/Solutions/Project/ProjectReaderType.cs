using System;

namespace SolutionGenerator.Toolkit.Solutions.Project
{
	/// <summary>
	/// Representes project reader type.
	/// </summary>
	[Serializable]
	public enum ProjectReaderType
	{
		/// <summary>
		/// Project will be parsed as plain XML and relevant data will be extracted.
		/// Note: some features of project files will not be supported (ex. imported projects).
		/// </summary>
		Xml,

		/// <summary>
		/// Project will be loaded as MSBuild Project and relevant data will be extracted.
		/// </summary>
		MsBuild
	}
}
