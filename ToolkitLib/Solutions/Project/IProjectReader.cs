using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SolutionGenerator.Toolkit.Solutions.Data;

namespace SolutionGenerator.Toolkit.Solutions.Project
{
	/// <summary>
	/// Represents the project reader.
	/// </summary>
	public interface IProjectReader
	{
		/// <summary>
		/// Loads the project.
		/// </summary>
		/// <returns></returns>
		VSProject LoadProject();

		/// <summary>
		/// Gets the included files.
		/// </summary>
		/// <returns></returns>
		IList<VSIncludedFile> IncludedFiles { get; }

		/// <summary>
		/// Gets the file references.
		/// </summary>
		IList<VSProjectReference> FileReferences { get; }

		/// <summary>
		/// Gets the file references.
		/// </summary>
		IList<VSProjectReference> ProjectReferences { get; }


		/// <summary>
		/// Gets the file references.
		/// </summary>
		IList<VSProjectReference> SilverlightProjectReferences { get; }
		
		/// <summary>
		/// Gets the output folder.
		/// </summary>
		string OutputFolder { get; }
	}
}
