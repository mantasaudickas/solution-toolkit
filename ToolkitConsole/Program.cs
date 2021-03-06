﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using SolutionGenerator.Toolkit.Logging;
using SolutionToolkit.Implementation;

namespace SolutionToolkit
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args == null || args.Length == 0)
                {
                    ShowHelp("No arguments provided");
                    Environment.ExitCode = -1;
                    return;
                }

                var arguments = new List<string>(args);
                if (arguments.Contains("-e"))
                {
                    WriteSampleConfigFile();
                    return;
                }

                int index = arguments.IndexOf("-v");
                if (index >= 0)
                {
                    if (arguments.Count <= index + 1)
                    {
                        ShowHelp("Configuration file is not specified");
                        Environment.ExitCode = -1;
                        return;
                    }

                    int verbosityLevel = int.Parse(arguments[index + 1]);
                    ConsoleLogger.VerbosityLevel = verbosityLevel;
                }

                index = arguments.IndexOf("-c");
                if ((index < 0 || arguments.Count <= index + 1) && (arguments.IndexOf("-s") >= 0))
                {
                    ShowHelp("Configuration file is not specified");
                    Environment.ExitCode = -1;
                    return;
                }

                var checkOnlyDependencies = false;

                string configurationFile = null;
                if (index >= 0)
                {
                    configurationFile = arguments[index + 1];
                    if (!File.Exists(configurationFile))
                    {
                        ShowHelp("Configuration file does not exists");
                        Environment.ExitCode = -1;
                        return;
                    }
                }

                string selectedProject = string.Empty;
                index = arguments.IndexOf("-p");
                if (index >= 0 && arguments.Count <= index + 1)
                {
                    ShowHelp("Project is not specified");
                    Environment.ExitCode = -1;
                    return;
                }
                else if (index >= 0)
                {
                    selectedProject = arguments[index + 1];
                }

                string thirdPartyOutputPath = string.Empty;
                index = arguments.IndexOf("-3");
                if (index >= 0 && arguments.Count <= index + 1)
                {
                    ShowHelp("Third parties output path is not specified");
                    Environment.ExitCode = -1;
                    return;
                }
                else if (index >= 0)
                {
                    thirdPartyOutputPath = arguments[index + 1];
                }

                string rootPath = ".";
                string thirdPartiesRootPath = ".";
                index = arguments.IndexOf("-r");
                if (index >= 0 && arguments.Count <= index + 2)
                {
                    ShowHelp("Root path is not specified");
                    Environment.ExitCode = -1;
                    return;
                }
                else if (index >= 0)
                {
                    rootPath = Path.GetFullPath(arguments[index + 1]);
                    thirdPartiesRootPath = Path.GetFullPath(arguments[index + 2]);
                }

                ProjectConfiguration configuration;

                if (!string.IsNullOrEmpty(configurationFile))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ProjectConfiguration));
                    configuration = (ProjectConfiguration) serializer.ReadObject(File.OpenRead(configurationFile));
                }
                else
                {
                    configuration = new ProjectConfiguration
                    {
                        RootPath = rootPath,
                        ThirdPartiesRootPath = thirdPartiesRootPath,
                        BinariesOutputPath = "Binaries",
                        Projects = new[] {new ProjectInfo {Name = selectedProject, Assemblies = new List<string> {selectedProject}}}
                    };

                    checkOnlyDependencies = true;
                }

                var configurationDirectory = Path.GetDirectoryName(configurationFile) ?? ".";
                if (!Path.IsPathRooted(configuration.RootPath))
                {
                    configuration.RootPath =
                        Path.GetFullPath(Path.Combine(configurationDirectory, configuration.RootPath));
                }

                if (!Path.IsPathRooted(configuration.ThirdPartiesRootPath))
                {
                    configuration.ThirdPartiesRootPath =
                        Path.GetFullPath(Path.Combine(configurationDirectory, configuration.ThirdPartiesRootPath));
                }

                IImplementation implementation;
                if (arguments.Contains("-f"))
                {
                    implementation = new ProjectFileModificationImplementation(checkOnlyDependencies);
                    implementation.Execute(configuration, selectedProject);
                }

                if (arguments.Contains("-s"))
                {
                    var outputIndex = arguments.IndexOf("-o");
                    if (outputIndex < 0 || arguments.Count <= outputIndex + 1)
                    {
                        ShowHelp("Solution output path is not specified");
                        Environment.ExitCode = -1;
                        return;
                    }

                    var solutionOutputPath = arguments[outputIndex + 1];

                    if (!Path.IsPathRooted(solutionOutputPath))
                    {
                        solutionOutputPath =
                            Path.GetFullPath(Path.Combine(configurationDirectory, solutionOutputPath));
                    }

                    var directory = Path.GetDirectoryName(solutionOutputPath);
                    if (directory != null && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    implementation = new SolutionGenerationImplementation(solutionOutputPath);
                    implementation.Execute(configuration, selectedProject);
                }

                if (arguments.Contains("-3"))
                {
                    implementation = new ThirdPartyCopyImplementation(thirdPartyOutputPath);
                    implementation.Execute(configuration, selectedProject);
                }
            }
            catch (Exception exc)
            {
                ConsoleLogger.Default.Fatal(exc.ToString());
            }
            finally
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("Press ENTER to exit");
                    Console.ReadLine();
                }
            }
        }

        private static void WriteSampleConfigFile()
        {
            ProjectConfiguration config = new ProjectConfiguration();
            config.RootPath = "..\\";
            config.ThirdPartiesRootPath = "..\\packages";
            config.BinariesOutputPath = "Binaries";
            config.TargetFrameworkVersion = "4.6.1";
            config.Projects = new[]
            {
                new ProjectInfo
                {
                    Name = "Project.Admin",
                    Assemblies = new List<string>
                    {
                        "Project.UI.Web",
                        "Project.Services.Scheduler",
                        "Project.UI.Web.Tests",
                        "Project.Business.Services.IntegrationTests",
                        "Project.Business.Services.Tests",
                        "DataAccess.Repositories.DataSeeder.Tests",
                        "DataAccess.Repositories.Readonly.Tests",
                    }
                },
                new ProjectInfo
                {
                    Name = "Project.SelfService",
                    Assemblies = new List<string>
                    {
                        "Project.SelfService.Api",
                        "Project.SelfService.Api.Tests"
                    }
                },
                new ProjectInfo
                {
                    Name = "Project.Integrations",
                    Assemblies = new List<string>
                    {
                        "Project.Integrations.Api",
                        "Integrations.Api.Tests",
                        "IntegrationApi.TestClient"
                    }
                },
            };

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ProjectConfiguration));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, config);

                var data = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                Console.WriteLine(data);
            }
        }

        private static void ShowHelp(string errorMessage)
        {
            Console.WriteLine(errorMessage);

            Console.WriteLine("-v - verbosity level [0-6]");
            Console.WriteLine("-s - generate solution");
            Console.WriteLine("-f - fix project files");
            Console.WriteLine("-e - show example project file");
            Console.WriteLine("-p <project name> - which project to use from configuration");
            Console.WriteLine("-c <path> - path to configuration file");
            Console.WriteLine("-o <path> - solution output path");

            // -f -c "d:\Projects\VIGO\VismaSchool\master\Builds\solution configuration.json"
            // -s -p Project.Admin -o ..\VismaSchool.SelfService.Gen.sln -c "d:\Projects\VIGO\VismaSchool\master\Builds\solution configuration.json"
            // -s -p Project.AdminWithIntegrations -o ..\VismaSchool.Gen.sln -c "d:\Projects\VIGO\VismaSchool\master\Builds\solution configuration.json"
            // -s -p Project.SelfService -o ..\VismaSchool.SelfService.Gen.sln -c "d:\Projects\VIGO\VismaSchool\master\Builds\solution configuration.json"
        }
    }
}
