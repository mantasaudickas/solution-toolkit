using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace SolutionGenerator.Toolkit.FileSystem
{
	public class FileSearcher : BaseComponent
	{
		private static readonly string executableFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
		private static readonly IDictionary<string, string[]> fileCache = new Dictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase);
		//private static readonly string fileCacheStorage;

		static FileSearcher()
		{
/*
			string location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
			fileCacheStorage = Path.Combine(location, "FileCache.xml");

			try
			{
				FileSystemInfo fileInfo = new FileInfo(fileCacheStorage);
				if (fileInfo.Exists)
				{
					FileRepository fileRepository = fileInfo.XmlDeserialize<FileRepository>();
					if (fileRepository != null && fileRepository.Groups != null)
					{
						foreach (FileInfoGroup infoGroup in fileRepository.Groups)
						{
							fileCache[infoGroup.Folder] = infoGroup.Files;
						}
					}
				}
			}
			catch (Exception e)
			{
				Logger.Error("Unable to deserialize file cache from {0}. Error message = {1}", fileCacheStorage, e.Message);
			}
*/
		}

		public FileSearcher(ILogger logger) : base(logger)
		{
		}

		internal static void ClearCache()
		{
			fileCache.Clear();
		}

		private static void SaveFileCache()
		{
/*
			FileRepository repository = new FileRepository();
			repository.Groups = new FileInfoGroup[fileCache.Count];

			int count = 0;
			foreach (string folder in fileCache.Keys)
			{
				FileInfoGroup infoGroup = new FileInfoGroup();
				infoGroup.Folder = folder;
				infoGroup.Files = fileCache[folder];
				repository.Groups[count] = infoGroup;
				++count;
			}

			string serializedRepository = repository.XmlSerialize();
			File.WriteAllText(fileCacheStorage, serializedRepository);
*/
		}

		public List<string> Scan(string searchPattern, string[] foldersToScan)
		{
			List<string> foundFiles = new List<string>();

			if (foldersToScan == null)
				return foundFiles;

			Stopwatch searchTimer = Stopwatch.StartNew();
			try
			{
				Logger.Info("");
				Logger.Info("Scanning file system. Search pattern = {0}{2}Folders = {1}{2}",
				            searchPattern, string.Join(Environment.NewLine, foldersToScan), Environment.NewLine);

				foreach (string folder in foldersToScan)
				{
					foundFiles.AddRange(Scan(searchPattern, folder));
				}
			}
			finally
			{
				searchTimer.Stop();
				Logger.Info("Source scan completed in {0}. Found {1} files.{2}",
					 searchTimer.Elapsed, foundFiles.Count, Environment.NewLine);
			}

			return foundFiles;
		}

		private IEnumerable<string> Scan(string searchPattern, string folder)
		{
			string key = Path.Combine(folder, searchPattern);

			string[] files;
			if (!fileCache.TryGetValue(key, out files))
			{
				Logger.Trace("Searching for files with pattern [{0}] in folder [{1}]", searchPattern, folder);
				files = Directory.GetFileSystemEntries(folder, searchPattern, SearchOption.AllDirectories);
				Logger.Trace("Found {0} files.", files.Length);

				fileCache[key] = files;
				SaveFileCache();
			}
			return files;
		}

		public string[] LocateFolders(string [] folders, params string [] probeFolders)
		{
			if (folders == null || folders.Length == 0)
				return new string[0];

			HashSet<string> folderSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			for (int i = 0; i < folders.Length; i++)
			{
				string locateFolder = LocateFolder(folders[i], probeFolders);
				folderSet.Add(locateFolder);
			}

			string [] foldersToReturn = new string[folderSet.Count];
			folderSet.CopyTo(foldersToReturn);
			return foldersToReturn;
		}

		public string LocateFolder(string folder, params string [] probeFolders)
		{
			if (Path.IsPathRooted(folder))
			{
				if (!Directory.Exists(folder))
					throw new SolutionGeneratorException("Folder [{0}] does not exists", folder);
				return folder;
			}

			Logger.Trace("Trying to locate folder [{0}]", folder);

			string located;
			if (probeFolders != null)
			{
				foreach (string probeFolder in probeFolders)
				{
					located = Path.GetFullPath(Path.Combine(probeFolder, folder));

					if (Directory.Exists(located))
					{
						Logger.Trace("Located at {0}", located);
						return located;
					}
					Logger.Trace("Checked at [{0}]", located);
				}
			}

			located = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, folder));
			if (Directory.Exists(located))
			{
				Logger.Trace("Located at {0}", located);
				return located;
			}

			located = Path.GetFullPath(Path.Combine(executableFolder, folder));
			if (Directory.Exists(located))
			{
				Logger.Trace("Located at {0}", located);
				return located;
			}

			Logger.Trace("Checked at [{0}]", located);

			throw new SolutionGeneratorException("Unable to locate folder [{0}]", folder);
		}

		public string[] LocateFiles(string[] files, params string[] probeFolders)
		{
			if (files == null || files.Length == 0)
				return new string[0];

			HashSet<string> fileSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			for (int i = 0; i < files.Length; i++)
			{
				string locateFile = LocateFile(files[i], probeFolders);
				fileSet.Add(locateFile);
			}

			string[] filesToReturn = new string[fileSet.Count];
			fileSet.CopyTo(filesToReturn);
			return filesToReturn;
		}

		public string LocateFile(string file, params string[] probeFolders)
		{
			if (string.IsNullOrEmpty(file))
				return file;

			if (Path.IsPathRooted(file))
			{
				if (!File.Exists(file))
					throw new SolutionGeneratorException("File [{0}] does not exists", file);
				return file;
			}

			Logger.Trace("Trying to locate file [{0}]", file);

			string located;
			if (probeFolders != null)
			{
				foreach (string probeFolder in probeFolders)
				{
					located = Path.GetFullPath(Path.Combine(probeFolder, file));
					if (File.Exists(located))
					{
						Logger.Trace("Located at {0}", located);
						return located;
					}
					Logger.Trace("Checked at [{0}]", located);
				}
			}

			located = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, file));
			if (File.Exists(located))
			{
				Logger.Trace("Located at {0}", located);
				return located;
			}
			Logger.Trace("Checked at [{0}]", located);

			located = Path.GetFullPath(Path.Combine(executableFolder, file));
			if (File.Exists(located))
			{
				Logger.Trace("Located at {0}", located); 
				return located;
			}
			Logger.Trace("Checked at [{0}]", located);

			throw new SolutionGeneratorException("Unable to locate file [{0}]", file);
		}

	}
}
