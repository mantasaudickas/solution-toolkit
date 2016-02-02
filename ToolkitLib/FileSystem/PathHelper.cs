using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SolutionGenerator.Toolkit.FileSystem
{
	/// <summary>
	/// Represents path helper.
	/// </summary>
	public class PathHelper
	{
		/// <summary>
		/// Pathes the combine.
		/// </summary>
		/// <param name="path">The path.</param>
		/// <param name="additionalPath">The additional path.</param>
		/// <returns></returns>
		public static string PathCombine(string path, string additionalPath)
		{
			if (string.IsNullOrEmpty(path))
				return additionalPath;

			if (string.IsNullOrEmpty(additionalPath))
				return path;

			string[] pathNodes = path.Split("\\".ToCharArray());
			string[] additionalPathNodes = additionalPath.Split("\\".ToCharArray());

			int count = 0;
			for (int i = 0; i < additionalPathNodes.Length; ++i)
			{
				if (additionalPathNodes[i] == "..")
					++count;
				else
					break;
			}

			if (count >= pathNodes.Length)
				return additionalPath;

			string newPath = string.Empty;
			int itemsToAdd = pathNodes.Length - count;
			for (int i = 0; i < itemsToAdd; ++i)
			{
				if (newPath.Length > 0)
					newPath += "\\";
				newPath += pathNodes[i];
			}

			string newAdditionalPath = string.Empty;
			for (int i = count; i < additionalPathNodes.Length; ++i)
			{
				if (newAdditionalPath.Length > 0)
					newAdditionalPath += "\\";
				newAdditionalPath += additionalPathNodes[i];
			}

			if (newPath.EndsWith(":"))
				newPath += "\\";

			string combinedPath = Path.Combine(newPath, newAdditionalPath);
			return combinedPath;
		}
	}
}
