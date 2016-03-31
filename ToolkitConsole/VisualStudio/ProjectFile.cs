using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace SolutionToolkit.VisualStudio
{
    public class ProjectFile
    {
        private const string DefaultNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

        private readonly XmlDocument _document;
        private IList<ProjectReference> _projectReferences;
        private IList<Reference> _references;
        private IList<PropertyGroup> _properties;
        private bool? isExecutable;

        public ProjectFile(string pathToFile)
        {
            if (pathToFile == null) throw new ArgumentNullException(nameof(pathToFile));
            if (string.IsNullOrWhiteSpace(pathToFile)) throw new ArgumentNullException(nameof(pathToFile));

            PathToFile = pathToFile;

            _document = new XmlDocument();
            _document.Load(pathToFile);

            DependentProjects = new HashSet<Guid>();
        }

        public string PathToFile { get; private set; }

        public IList<ProjectReference> ProjectReferences
        {
            get
            {
                if (_projectReferences == null)
                {
                    _projectReferences = LoadProjectReferences();
                }
                return _projectReferences;
            }
        }

        public IList<Reference> References
        {
            get
            {
                if (_references == null)
                {
                    _references = LoadReferences();
                }
                return _references;
            }
        }

        public IList<PropertyGroup> Properties
        {
            get
            {
                if (_properties == null)
                {
                    _properties = LoadProperties();
                }
                return _properties;
            }
        }

        public bool IsExecutable
        {
            get
            {
                if (!isExecutable.HasValue)
                {
                    isExecutable = ResolveIsExecutable();
                }
                return isExecutable.Value;
            }
        }

        private bool ResolveIsExecutable()
        {
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            var node = _document.SelectSingleNode(GetXPath(@"OutputType"), namespaceManager);
            if (node == null)
                return false;
            return string.Equals(node.InnerText, "WinExe", StringComparison.InvariantCultureIgnoreCase) || string.Equals(node.InnerText, "Exe", StringComparison.InvariantCultureIgnoreCase);
        }

        public HashSet<Guid> DependentProjects { get; private set; }

        public void AddReference(string include, string hintPath)
        {
            var documentElement = _document.DocumentElement;
            if (documentElement == null)
                return;

            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList itemGroups = _document.SelectNodes(GetXPath(@"ItemGroup"), namespaceManager);

            XmlNode parentNode = null;
            if (itemGroups != null)
            {
                foreach (XmlNode itemGroup in itemGroups)
                {
                    if (itemGroup.FirstChild.Name == "Reference")
                    {
                        parentNode = itemGroup;
                        break;
                    }
                }
            }

            if (parentNode == null)
            {
                parentNode = documentElement.AppendChild(_document.CreateElement("ItemGroup", DefaultNamespace));
            }

            var referenceElement = _document.CreateElement("Reference", DefaultNamespace);

            var includeAttribute = _document.CreateAttribute("Include");
            includeAttribute.Value = include;
            referenceElement.Attributes.Append(includeAttribute);

            if (!string.IsNullOrEmpty(hintPath))
            {
                var hintPathNode = _document.CreateElement("HintPath", DefaultNamespace);
                hintPathNode.InnerText = hintPath;
                referenceElement.AppendChild(hintPathNode);
            }

            parentNode.AppendChild(referenceElement);
        }

        public bool SetReferencePrivacy(bool isPrivate, string [] path)
        {
            var changed = false;
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList referenceNodes = _document.SelectNodes(GetXPath(@"Reference"), namespaceManager);
            if (referenceNodes != null)
            {
                foreach (XmlNode node in referenceNodes)
                {
                    var hintPathNode = SelectChildNode(node, "HintPath");
                    if (hintPathNode == null)
                        continue;

                    var hintPath = hintPathNode.InnerText.ToLowerInvariant();
                    var match = path.Any(p => hintPath.ToLowerInvariant().Contains(p.ToLowerInvariant()));

                    if (!match)
                        continue;

                    XmlNode privateNode = SelectChildNode(node, "Private");
                    if (privateNode == null)
                    {
                        privateNode = _document.CreateElement("Private", DefaultNamespace);
                        node.AppendChild(privateNode);
                        changed = true;
                    }
                    var nextValue = isPrivate ? "True" : "False";
                    if (privateNode.InnerText != nextValue)
                    {
                        privateNode.InnerText = nextValue;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        public bool SetReferenceSpecificVersion(ReferenceChangeMode specificVersionMode)
        {
            if (specificVersionMode == ReferenceChangeMode.None)
                return false;

            var changed = false;
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList referenceNodes = _document.SelectNodes(GetXPath(@"Reference"), namespaceManager);
            if (referenceNodes != null)
            {
                foreach (XmlNode node in referenceNodes)
                {
                    var hasVersionSpecified = false;
                    var include = node.Attributes?["Include"];
                    if (include != null)
                    {
                        var assemblyName = include.InnerText;
                        hasVersionSpecified = assemblyName.Contains(",");
                    }

                    var specificVersionNode = SelectChildNode(node, "SpecificVersion");

                    if (specificVersionMode == ReferenceChangeMode.Remove && !hasVersionSpecified)
                    {
                        var parent = specificVersionNode?.ParentNode;
                        parent?.RemoveChild(specificVersionNode);
                        changed = true;
                    }
                    else
                    {
                        if (specificVersionNode == null)
                        {
                            specificVersionNode = _document.CreateElement("SpecificVersion", DefaultNamespace);
                            node.AppendChild(specificVersionNode);
                            changed = true;
                        }

                        var newValue = specificVersionMode == ReferenceChangeMode.Add ? "True" : "False";
                        if (specificVersionNode.InnerText != newValue)
                        {
                            specificVersionNode.InnerText = newValue;
                            changed = true;
                        }
                    }
                }
            }
            return changed;
        }

        public bool SetOutputPath(string outputPath)
        {
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList nodes = _document.SelectNodes(GetXPath(@"OutputPath"), namespaceManager);

            if (nodes == null)
                throw new Exception("Output path not found!");

            var changed = false;
            foreach (XmlNode node in nodes)
            {
                if (!string.Equals(node.InnerText, outputPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    node.InnerText = outputPath;
                    changed = true;
                }
            }
            return changed;
        }

        public bool SetTargetFrameworkVersion(string version)
        {
            var changed = false;
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList nodes = _document.SelectNodes(GetXPath(@"TargetFrameworkVersion"), namespaceManager);

            if (nodes == null)
                throw new Exception("TargetFrameworkVersion not found!");

            foreach (XmlNode node in nodes)
            {
                if (node.InnerText != version)
                {
                    changed = true;
                    node.InnerText = version;
                }
            }

            return changed;
        }

        public bool ChangePlatform(string platform)
        {
            var changed = false;
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList nodes = _document.SelectNodes(GetXPath(@"Platform"), namespaceManager);

            if (nodes == null)
                throw new Exception("Platform not found!");

            foreach (XmlNode node in nodes)
            {
                if (node.InnerText != platform)
                {
                    changed = true;
                    node.InnerText = platform;
                }
            }

            return changed;
        }


        public bool RemoveProjectReferences()
        {
            var changed = false;
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList projectReferences = _document.SelectNodes(GetXPath(@"ProjectReference"), namespaceManager);
            if (projectReferences != null && projectReferences.Count > 0)
            {
                foreach (XmlNode projectReference in projectReferences)
                {
                    var parent = projectReference.ParentNode;
                    if (parent != null)
                    {
                        changed = true;
                        parent.RemoveChild(projectReference);
                    }
                }
            }
            return changed;
        }

        public bool RemoveSystemRuntimeReference()
        {
            var changed = false;
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList references = _document.SelectNodes(GetXPath(@"Reference"), namespaceManager);
            if (references != null && references.Count > 0)
            {
                foreach (XmlNode reference in references)
                {
                    var isSystemRuntimeReference = false;
                    if (reference.Attributes != null)
                    {
                        foreach (XmlAttribute attribute in reference.Attributes)
                        {
                            if (attribute.Name == "Include" && (attribute.Value.StartsWith("System.Runtime,") || attribute.Value.Equals("System.Runtime")))
                            {
                                isSystemRuntimeReference = true;
                            }
                        }
                    }

                    if (isSystemRuntimeReference)
                    {
                        var parent = reference.ParentNode;
                        parent?.RemoveChild(reference);
                        changed = true;
                    }
                }
            }
            return changed;
        }

        public bool AddSystemRuntimeReference()
        {
            var changed = false;
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList references = _document.SelectNodes(GetXPath(@"Reference"), namespaceManager);
            if (references != null && references.Count > 0)
            {
                var exists = false;
                var isValid = false;
                foreach (XmlNode reference in references)
                {
                    if (reference.Attributes != null)
                    {
                        foreach (XmlAttribute attribute in reference.Attributes)
                        {
                            if (attribute.Name == "Include" && (attribute.Value.StartsWith("System.Runtime,") || attribute.Value.Equals("System.Runtime")))
                            {
                                exists = true;
                                isValid = SelectChildNode(reference, "HintPath") != null;
                                break;
                            }
                        }

                        if (exists && !isValid)
                        {
                            var parent = reference.ParentNode;
                            if (parent != null)
                            {
                                parent.RemoveChild(reference);
                            }
                            exists = false;
                            break;
                        }
                    }
                }

                if (!exists)
                {
                    AddReference("System.Runtime", @"c:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\Facades\System.Runtime.dll");
                    changed = true;
                }
            }
            return changed;
        }

        public bool RemoveNuget()
        {
            var changed = false;
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList nodeList = _document.SelectNodes(GetXPath(@"RestorePackages"), namespaceManager);
            if (nodeList != null && nodeList.Count > 0)
            {
                foreach (XmlNode projectReference in nodeList)
                {
                    var parent = projectReference.ParentNode;
                    if (parent != null)
                    {
                        changed = true;
                        parent.RemoveChild(projectReference);
                    }
                }
            }

            nodeList = _document.SelectNodes(GetXPath(@"Import"), namespaceManager);
            if (nodeList != null && nodeList.Count > 0)
            {
                foreach (XmlNode node in nodeList)
                {
                    if (node.Attributes == null)
                        continue;

                    if (node.Attributes["Project"] != null && node.Attributes["Project"].Value == "$(SolutionDir)\\.nuget\\NuGet.targets")
                    {
                        var parent = node.ParentNode;
                        if (parent != null)
                        {
                            changed = true;
                            parent.RemoveChild(node);
                        }
                    }
                }
            }

            nodeList = _document.SelectNodes(GetXPath(@"Target"), namespaceManager);
            if (nodeList != null && nodeList.Count > 0)
            {
                foreach (XmlNode node in nodeList)
                {
                    if (node.Attributes == null)
                        continue;

                    if (node.Attributes["Name"] != null && node.Attributes["Name"].Value == "EnsureNuGetPackageBuildImports")
                    {
                        var parent = node.ParentNode;
                        if (parent != null)
                        {
                            changed = true;
                            parent.RemoveChild(node);
                        }
                    }
                }
            }
            return changed;
        }

        public void Save()
        {
            _document.Save(PathToFile);
        }

        public string CalculateRelativePath(string rootPath)
        {
            var path = string.Empty;

            var projectPath = PathToFile.Substring(rootPath.Length+1);

            var pathNodes = projectPath.Split("\\".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            projectPath = string.Empty;

            int nodeCount = pathNodes.Length - 1;
            for (int i = 0; i < nodeCount; ++i)
            {
                path += "..\\";
                projectPath += pathNodes[i] + "\\";
            }

            return path;
        }

        public string GetAssemblyName()
        {
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList nodeList = _document.SelectNodes(GetXPath(@"AssemblyName"), namespaceManager);
            if (nodeList == null)
                throw new Exception("AssemblyName node not found!");

            var firstNode = nodeList[0];
            return firstNode.InnerText;
        }

        public string GetOutputPath()
        {
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList nodeList = _document.SelectNodes(GetXPath(@"OutputPath"), namespaceManager);
            if (nodeList == null)
                throw new Exception("OutputPath node not found!");

            var firstNode = nodeList[0];
            return firstNode.InnerText;
        }

        public string GetProjectId()
        {
            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList nodeList = _document.SelectNodes(GetXPath(@"ProjectGuid"), namespaceManager);
            if (nodeList == null)
                throw new Exception("ProjectGuid node not found!");

            var firstNode = nodeList[0];
            return firstNode.InnerText;
        }

        private IList<ProjectReference> LoadProjectReferences()
        {
            var references = new List<ProjectReference>();

            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList projectReferences = _document.SelectNodes(GetXPath(@"ProjectReference"), namespaceManager);
            if (projectReferences != null)
            {
                foreach (XmlNode node in projectReferences)
                {
                    var xml = node.OuterXml;
                    var reference = Deserialize<ProjectReference>(xml);
                    references.Add(reference);
                }
            }

            return references;
        }

        private IList<Reference> LoadReferences()
        {
            var references = new List<Reference>();

            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList referenceNodes = _document.SelectNodes(GetXPath(@"Reference"), namespaceManager);
            if (referenceNodes != null)
            {
                foreach (XmlNode node in referenceNodes)
                {
                    var xml = node.OuterXml;
                    var reference = Deserialize<Reference>(xml);
                    references.Add(reference);
                }
            }

            return references;
        }

        private IList<PropertyGroup> LoadProperties()
        {
            var properties = new List<PropertyGroup>();

            XmlNamespaceManager namespaceManager = GetNamespaceManager(_document);
            XmlNodeList groups = _document.SelectNodes(GetXPath(@"PropertyGroup"), namespaceManager);
            if (groups != null)
            {
                foreach (XmlNode node in groups)
                {
                    var xml = node.OuterXml;
                    var reference = Deserialize<PropertyGroup>(xml);
                    properties.Add(reference);
                }
            }

            return properties;
        }

        private T Deserialize<T>(string xml)
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T), DefaultNamespace);
                return (T) serializer.Deserialize(stream);
            }
        }

        private static XmlNode SelectChildNode(XmlNode node, string nodeName)
        {
            XmlNode hintPathNode = null;
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.Name == nodeName)
                {
                    hintPathNode = childNode;
                    break;
                }
            }
            return hintPathNode;
        }

        private static string GetXPath(string propertyName)
        {
            return @"//vs:" + propertyName;
        }

        private static XmlNamespaceManager GetNamespaceManager(XmlDocument xmlDocument)
        {
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            namespaceManager.AddNamespace("vs", DefaultNamespace);
            return namespaceManager;
        }
    }
}
