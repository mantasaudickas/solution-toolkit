﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace SolutionToolkit
{
    public class ProjectConfiguration
    {
        public string RootPath { get; set; }
        public string ThirdPartiesRootPath { get; set; }
        public string BinariesOutputPath { get; set; }
        public string TargetFrameworkVersion { get; set; }
        public string SystemRuntimeReferenceMode { get; set; }
        public string SpecificVersionMode { get; set; }
        public ProjectInfo [] Projects { get; set; }

        public string[] ResolveAssemblies(string selectedProject)
        {
            if (Projects == null)
                return new string[0];

            return Projects
                .Where(info => string.IsNullOrWhiteSpace(selectedProject) || info.Name.Equals(selectedProject, StringComparison.InvariantCultureIgnoreCase))
                .SelectMany(info => info.Assemblies)
                .ToArray();
        }

        public ReferenceChangeMode GetSystemRuntimeReferenceMode
        {
            get
            {
                ReferenceChangeMode mode;
                if (!Enum.TryParse(SystemRuntimeReferenceMode, out mode))
                    mode = ReferenceChangeMode.None;
                return mode;
            }
        }

        public ReferenceChangeMode GetSpecificVersionReferenceMode
        {
            get
            {
                ReferenceChangeMode mode;
                if (!Enum.TryParse(SpecificVersionMode, out mode))
                    mode = ReferenceChangeMode.None;
                return mode;
            }
        }
    }

    public class ProjectInfo
    {
        public string Name { get; set; }
        public IList<string> Assemblies { get; set; }
    }

    public enum ReferenceChangeMode
    {
        None,
        Add,
        Remove
    }
}
