using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Evaluation;

using SolutionGenerator.Toolkit.Solutions.Data;

namespace SolutionGenerator.Toolkit.Solutions.Project
{
    /// <summary>
    /// Represents ms build project reader.
    /// </summary>
    public class MsBuildProjectReader : IProjectReader
    {
        #region CONSTANTS

        private const string REFERENCE = "Reference";
        private const string REFERENCE_HINT_PATH = "HintPath";
        private const string PROJECT_REFERENCE = "ProjectReference";
        private const string PROJECT_REFERENCE_ID = "Project";
        private const string SILVERLIGHT_REFERENCE_LIST = "SilverlightApplicationList";
        private const string INCLUDE_COMPILE = "Compile";
        private const string INCLUDE_LINK = "Link";

        #endregion

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
        private Microsoft.Build.Evaluation.Project projectDocument;

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
        /// Initializes a new instance of the <see cref="MsBuildProjectReader"/> class.
        /// </summary>
        /// <param name="projectLocation">The project location.</param>
        public MsBuildProjectReader(string projectLocation)
        {
            if (String.IsNullOrEmpty(projectLocation))
                throw new ArgumentNullException(projectLocation, "Project location cannot be null for MsBuildProjectReader.");

            this.projectLocation = projectLocation;
        }

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the project document.
        /// </summary>
        private Microsoft.Build.Evaluation.Project ProjectDocument
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
                // NOTE: if using XmlReader, property $(MSBuildProjectDirectory) is set to executable location, not project
                //using (XmlReader reader = new XmlTextReader(projectLocation))
                {
                    this.projectDocument = new Microsoft.Build.Evaluation.Project(/*reader*/projectLocation);
                    
                    this.project = new VSProject();
                    this.project.ProjectFileLocation = projectLocation;

                    this.project.ProjectId = new Guid(this.GetEvaluatedProperty(this.projectDocument, "ProjectGuid", true));
                    this.project.AssemblyName = this.GetEvaluatedProperty(this.projectDocument, "AssemblyName", false);
                    this.project.RootNamespace = this.GetEvaluatedProperty(this.projectDocument, "RootNamespace", false);
                    string outputTypeProperty = this.GetEvaluatedProperty(this.projectDocument, "OutputType", false);
                    this.project.OutputType = (VSProjectOutputType)Enum.Parse(typeof(VSProjectOutputType), outputTypeProperty, true);
                    this.project.OutputPath = this.GetEvaluatedProperty(this.projectDocument, "OutputPath", false);
                    this.project.AssemblyOriginatorKeyFile = this.GetEvaluatedProperty(this.projectDocument, "AssemblyOriginatorKeyFile", true);

                    if (string.IsNullOrEmpty(project.AssemblyName))
                        project.AssemblyName = project.RootNamespace;
                    
                    string projectFileFolder = Path.GetDirectoryName(projectLocation) ?? ".";
                    this.outputFolder = Path.GetFullPath(FileSystem.PathHelper.PathCombine(projectFileFolder, this.project.OutputPath));
                    this.project.ResolvedOutput = FileSystem.PathHelper.PathCombine(this.outputFolder, this.project.AssemblyName);

                    if (this.project.OutputType == VSProjectOutputType.Exe || this.project.OutputType == VSProjectOutputType.WinExe)
                        this.project.ResolvedOutput += ".exe";
                    else
                        this.project.ResolvedOutput += ".dll";
                    
                    this.fileReferences = new List<VSProjectReference>(50);
                    this.projectReferences = new List<VSProjectReference>(5);
                    
                    this.GetReferences(this.projectDocument, ref this.fileReferences, ref this.projectReferences);
                    this.silverlightProjectReferences = this.GetSilverlightReferences(this.projectDocument);

                    List<VSProjectReference> references = new List<VSProjectReference>(fileReferences.Count + projectReferences.Count + silverlightProjectReferences.Count);
                    references.AddRange(fileReferences);
                    references.AddRange(projectReferences);
                    references.AddRange(silverlightProjectReferences);
                    project.References = references.ToArray();

                    this.includeFiles = this.GetIncludedFiles(this.projectDocument);

                    this.project.IsLauncher = this.project.OutputType != VSProjectOutputType.Library;
                    var webconfig = Path.Combine(projectFileFolder, "Web.config");
                    if (File.Exists(webconfig))
                        this.project.IsLauncher = true;
                }
            }

            return this.project;
        }

        #endregion

        #region MSBUILD PROJECT HANDLING

        /// <summary>
        /// Gets the references.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="references">The references.</param>
        /// <param name="projectReferences">The project references.</param>
        private void GetReferences(Microsoft.Build.Evaluation.Project project, ref IList<VSProjectReference> references, ref IList<VSProjectReference> projectReferences)
        {
            foreach (ProjectItem projectItem in project.AllEvaluatedItems)
            {
                if (projectItem.ItemType == REFERENCE || projectItem.ItemType == PROJECT_REFERENCE)
                {
                    VSProjectReference reference = new VSProjectReference();
                    reference.Include = projectItem.EvaluatedInclude;

                    if (projectItem.ItemType == PROJECT_REFERENCE)
                    {
                        reference.IsProjectReference = true;
                        reference.ProjectReferenceId = new Guid(projectItem.GetMetadataValue(PROJECT_REFERENCE_ID));
                        projectReferences.Add(reference);
                    }
                    else
                    {
                        reference.HintPath = projectItem.GetMetadataValue(REFERENCE_HINT_PATH);

                        if (reference.HintPath == string.Empty)
                            reference.HintPath = null;
                        
                        references.Add(reference);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the silverlight references.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns></returns>
        private IList<VSProjectReference> GetSilverlightReferences(Microsoft.Build.Evaluation.Project project)
        {
            string referenceList = GetEvaluatedProperty(project, SILVERLIGHT_REFERENCE_LIST, true);
            return ManualProjectReader.ParseSilverlightReferences(referenceList);
        }

        /// <summary>
        /// Gets the evaluated property.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="name">The name.</param>
        /// <param name="first">if set to <c>true</c> [first].</param>
        /// <returns></returns>
        private string GetEvaluatedProperty(Microsoft.Build.Evaluation.Project project, string name, bool first)
        {
            string result = null;

            foreach (ProjectProperty property in project.AllEvaluatedProperties)
            {
                if (property.Name == name)
                    result = property.EvaluatedValue;

                if (first && !String.IsNullOrEmpty(result))
                    break;
            }

            return result;
        }

        /// <summary>
        /// Gets the included files.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns></returns>
        private IList<VSIncludedFile> GetIncludedFiles(Microsoft.Build.Evaluation.Project project)
        {
            IList<VSIncludedFile> result = new List<VSIncludedFile>();

            foreach (ProjectItem projectItem in project.AllEvaluatedItems)
            {
                if(projectItem.ItemType == INCLUDE_COMPILE)
                {
                    VSIncludedFile included = new VSIncludedFile();
                    included.Include = projectItem.EvaluatedInclude;

                    if(projectItem.HasMetadata(INCLUDE_LINK))
                    {
                        included.IsLink = true;
                        included.LinkTarget = projectItem.GetMetadataValue(INCLUDE_LINK);
                    }
                    result.Add(included);
                }
            }

            return result;
        }

        #endregion
    }
}
