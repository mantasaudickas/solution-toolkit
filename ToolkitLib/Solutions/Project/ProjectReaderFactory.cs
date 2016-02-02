using System;

namespace SolutionGenerator.Toolkit.Solutions.Project
{
	/// <summary>
	/// Represents project reader factory.
	/// </summary>
	public class ProjectReaderFactory
	{
		/// <summary>
		/// Creates the project reader.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="projectLocation">The project location.</param>
		/// <returns></returns>
		public static IProjectReader CreateProjectReader(ProjectReaderType type, string projectLocation)
		{
			IProjectReader reader = null;

			if (String.IsNullOrEmpty(projectLocation))
				throw new ArgumentNullException(projectLocation, "Project location cannot be null while creating project reader.");

			if(type == ProjectReaderType.MsBuild)
				reader = new MsBuildProjectReader(projectLocation);
			else
				reader = new ManualProjectReader(projectLocation);

			return reader;
		}
	}
}
