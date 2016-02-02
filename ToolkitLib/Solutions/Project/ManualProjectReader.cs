using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using SolutionGenerator.Toolkit.Solutions.Data;

namespace SolutionGenerator.Toolkit.Solutions.Project
{
	/// <summary>
	/// Represents manual project reader.
	/// </summary>
	public class ManualProjectReader : IProjectReader
	{
		#region FIELDS

		/// <summary>
		/// Contains reference to the project location.
		/// </summary>
		private readonly string projectLocation;

		/// <summary>
		/// Contains reference to the project.
		/// </summary>
		private VSProject project;

		/// <summary>
		/// Contains reference to the project document.
		/// </summary>
		private XmlDocument projectDocument;

		/// <summary>
		/// Contains reference to the output folder.
		/// </summary>
		private string outputFolder;

		/// <summary>
		/// Contains reference to the include files.
		/// </summary>
		private IList<VSIncludedFile> includeFiles;

		/// <summary>
		/// Contains reference to the file references.
		/// </summary>
		private IList<VSProjectReference> fileReferences;

		/// <summary>
		/// Contains reference to the project references.
		/// </summary>
		private IList<VSProjectReference> projectReferences;

		/// <summary>
		/// Contains reference to the silverlight project references.
		/// </summary>
		private IList<VSProjectReference> silverlightProjectReferences;

		#endregion

		#region CONSTRUCTOR

		/// <summary>
		/// Initializes a new instance of the <see cref="ManualProjectReader"/> class.
		/// </summary>
		/// <param name="projectLocation">The project location.</param>
		public ManualProjectReader(string projectLocation)
		{
			if (String.IsNullOrEmpty(projectLocation))
				throw new ArgumentNullException(projectLocation, "Project location cannot be null for ManualProjectReader.");

			this.projectLocation = projectLocation;
		}

		#endregion

		#region PROPERTIES

		/// <summary>
		/// Gets the project document.
		/// </summary>
		private XmlDocument ProjectDocument
		{
			get
			{
				if (this.projectDocument == null)
					this.LoadProject();

				return this.projectDocument;
			}
		}

		/// <summary>
		/// Gets the output folder.
		/// </summary>
		public string OutputFolder
		{
			get
			{
				if (String.IsNullOrEmpty(this.outputFolder))
					this.LoadProject();

				return this.outputFolder;
			}
		}

		/// <summary>
		/// Gets the included files.
		/// </summary>
		/// <returns></returns>
		public IList<VSIncludedFile> IncludedFiles
		{
			get
			{
				if (this.includeFiles == null)
					this.LoadProject();

				return this.includeFiles;
			}
		}

		/// <summary>
		/// Gets the file references.
		/// </summary>
		public IList<VSProjectReference> FileReferences
		{
			get
			{
				if (this.fileReferences == null)
					this.LoadProject();

				return this.fileReferences;
			}
		}

		/// <summary>
		/// Gets the file references.
		/// </summary>
		public IList<VSProjectReference> ProjectReferences
		{
			get
			{
				if (this.projectReferences == null)
					this.LoadProject();

				return this.projectReferences;
			}
		}

		/// <summary>
		/// Gets the file references.
		/// </summary>
		public IList<VSProjectReference> SilverlightProjectReferences
		{
			get
			{
				if (this.silverlightProjectReferences == null)
					this.LoadProject();

				return this.silverlightProjectReferences;
			}
		}

		#endregion

		#region LOAD

		/// <summary>
		/// Loads the project.
		/// </summary>
		/// <returns></returns>
		public VSProject LoadProject()
		{
			if (this.project == null)
			{
				this.projectDocument = new XmlDocument();
				this.projectDocument.Load(projectLocation);

				this.project = new VSProject();
				this.project.ProjectFileLocation = projectLocation;

				this.project.ProjectId = new Guid(SelectSingleValue(this.projectDocument, "ProjectGuid", true));
				this.project.AssemblyName = SelectSingleValue(this.projectDocument, "AssemblyName");
				this.project.RootNamespace = SelectSingleValue(this.projectDocument, "RootNamespace", true);
				string outputTypeProperty = SelectSingleValue(this.projectDocument, "OutputType", true);
				this.project.OutputType = (VSProjectOutputType) Enum.Parse(typeof (VSProjectOutputType), outputTypeProperty, true);
				this.project.OutputPath = SelectSingleValue(this.projectDocument, "OutputPath");
				this.project.AssemblyOriginatorKeyFile = SelectSingleValue(this.projectDocument, "AssemblyOriginatorKeyFile");

				if (string.IsNullOrEmpty(project.AssemblyName))
					project.AssemblyName = project.RootNamespace;

				string projectFileFolder = Path.GetDirectoryName(projectLocation) ?? ".";
				this.outputFolder = Path.GetFullPath(FileSystem.PathHelper.PathCombine(projectFileFolder, this.project.OutputPath));
				this.project.ResolvedOutput = FileSystem.PathHelper.PathCombine(this.outputFolder, this.project.AssemblyName);

				if (this.project.OutputType == VSProjectOutputType.Exe || this.project.OutputType == VSProjectOutputType.WinExe)
					this.project.ResolvedOutput += ".exe";
				else
					this.project.ResolvedOutput += ".dll";

				this.fileReferences = GetFileReferences(this.projectDocument);
				this.projectReferences = GetProjectReferences(this.projectDocument);
				this.silverlightProjectReferences = GetSilverlightProjectReferences(this.projectDocument);

				List<VSProjectReference> references = new List<VSProjectReference>(fileReferences.Count + projectReferences.Count + silverlightProjectReferences.Count);
				references.AddRange(fileReferences);
				references.AddRange(projectReferences);
				references.AddRange(silverlightProjectReferences);
				project.References = references.ToArray();

				this.includeFiles = this.GetIncludedFiles();
			}

			return this.project;
		}

		#endregion

		#region XML HANDLING

		private static IList<VSProjectReference> GetFileReferences(XmlDocument xmlDocument)
		{
			return GetReferences(xmlDocument, GetXPath(@"Reference"), false);
		}

		private static IList<VSProjectReference> GetProjectReferences(XmlDocument xmlDocument)
		{
			return GetReferences(xmlDocument, GetXPath(@"ProjectReference"), true);
		}

		private static IList<VSProjectReference> GetSilverlightProjectReferences(XmlDocument xmlDocument)
		{
			return GetSilverlightReferences(xmlDocument, GetXPath(@"SilverlightApplicationList"));
		}

		private static IList<VSProjectReference> GetReferences(XmlDocument xmlDocument, string xpath, bool isProjectReference)
		{
			XmlNamespaceManager namespaceManager = GetNamespaceManager(xmlDocument);
			XmlNodeList nodeList = xmlDocument.SelectNodes(xpath, namespaceManager);

			List<VSProjectReference> references = new List<VSProjectReference>();
			if (nodeList != null)
			{
				foreach (XmlNode node in nodeList)
				{
					string include = SelectAttributeValue(node, "Include");
					string hintPath = null;
					Guid? projectReferenceId = null;
					foreach (XmlNode childNode in node.ChildNodes)
					{
						if (childNode.Name == "HintPath")
						{
							hintPath = childNode.InnerXml;
						}
						else if (childNode.Name == "Project")
						{
							projectReferenceId = new Guid(childNode.InnerXml);
						}
					}

					VSProjectReference reference = new VSProjectReference();
					reference.Include = include;
					reference.HintPath = hintPath;
					reference.IsProjectReference = isProjectReference;
					reference.ProjectReferenceId = projectReferenceId.GetValueOrDefault();
					references.Add(reference);
				}
			}
			return references;
		}
		
		private static IList<VSProjectReference> GetSilverlightReferences(XmlDocument xmlDocument, string xpath)
		{
			XmlNamespaceManager namespaceManager = GetNamespaceManager(xmlDocument);
			XmlNode singleNode = xmlDocument.SelectSingleNode(xpath, namespaceManager);

			string referenceList = singleNode != null ? singleNode.InnerText : null;
			return ParseSilverlightReferences(referenceList);
		}

		/// <summary>
		/// Parses the silverlight references.
		/// </summary>
		/// <param name="referenceList">The reference list.</param>
		/// <returns></returns>
		public static IList<VSProjectReference> ParseSilverlightReferences(string referenceList)
		{
			List<VSProjectReference> references = new List<VSProjectReference>();
			if (!string.IsNullOrEmpty(referenceList))
			{
				string[] projectNodes = referenceList.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
				foreach (string projectNode in projectNodes)
				{
					string[] nodes = projectNode.Split("|".ToCharArray());
					if (nodes.Length >= 2)
					{
						VSProjectReference reference = new VSProjectReference();
						reference.HintPath = nodes[1];
						reference.IsSilverlightReference = true;
						reference.ProjectReferenceId = new Guid(nodes[0]);
						references.Add(reference);
					}
				}
			}
			return references;
		}

		private static string SelectAttributeValue(XmlNode xmlNode, string attributeName)
		{
			string attributeValue = null;
			if (xmlNode != null && xmlNode.Attributes != null)
			{
				attributeValue =
					(from XmlAttribute xmlAttribute in xmlNode.Attributes
					 where xmlAttribute.Name == attributeName
					 select xmlAttribute.Value).FirstOrDefault();
			}
			return attributeValue;
		}

		private static string SelectSingleValue(XmlDocument xmlDocument, string propertyName, bool failWhenNullOrEmpty = false)
		{
			XmlNode selectedNode = SelectSingleNode(xmlDocument, propertyName, failWhenNullOrEmpty);

			string propertyValue = selectedNode != null ? selectedNode.InnerXml : null;

			if (failWhenNullOrEmpty && string.IsNullOrWhiteSpace(propertyValue))
				throw new ApplicationException(string.Format("Property [{0}] is empty.", propertyName));

			return propertyValue;
		}

		private static XmlNode SelectSingleNode(XmlDocument xmlDocument, string propertyName, bool failWhenNullOrEmpty = false)
		{
			XmlNamespaceManager namespaceManager = GetNamespaceManager(xmlDocument);
			XmlNode selectedNode = xmlDocument.SelectSingleNode(GetXPath(propertyName), namespaceManager);

			if (failWhenNullOrEmpty && selectedNode == null)
				throw new ApplicationException(string.Format("Property [{0}] not found.", propertyName));

			return selectedNode;
		}

		private static XmlNamespaceManager GetNamespaceManager(XmlDocument xmlDocument)
		{
			XmlNamespaceManager namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
			namespaceManager.AddNamespace("vs", "http://schemas.microsoft.com/developer/msbuild/2003");
			return namespaceManager;
		}

		private static string GetXPath(string propertyName)
		{
			return @"//vs:" + propertyName;
		}

		/// <summary>
		/// Gets the included files.
		/// </summary>
		/// <returns></returns>
		private IList<VSIncludedFile> GetIncludedFiles()
		{
			XmlDocument xmlDocument = this.ProjectDocument;

			string xpath = GetXPath(@"Compile");
			XmlNamespaceManager namespaceManager = GetNamespaceManager(xmlDocument);
			XmlNodeList nodeList = xmlDocument.SelectNodes(xpath, namespaceManager);

			List<VSIncludedFile> files = new List<VSIncludedFile>();
			if (nodeList != null)
			{
				foreach (XmlNode node in nodeList)
				{
					string include = SelectAttributeValue(node, "Include");
					bool isLink = false;
					string linkTarget = null;
					foreach (XmlNode childNode in node.ChildNodes)
					{
						if (childNode.Name == "Link")
						{
							isLink = true;
							linkTarget = childNode.InnerXml;
						}
					}

					VSIncludedFile file = new VSIncludedFile { Include = include, IsLink = isLink, LinkTarget = linkTarget };
					files.Add(file);
				}
			}
			return files;
		}

		#endregion
	}
}
