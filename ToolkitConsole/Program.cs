using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
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

                int index = arguments.IndexOf("-c");
                if ((index < 0 || arguments.Count <= index + 1) && (arguments.IndexOf("-s") >= 0 || arguments.IndexOf("-f") >= 0))
                {
                    ShowHelp("Configuration file is not specified");
                    Environment.ExitCode = -1;
                    return;
                }

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
                    configuration =
                        JsonConvert.DeserializeObject<ProjectConfiguration>(File.ReadAllText(configurationFile));
                }
                else
                {
                    configuration = new ProjectConfiguration();
                    configuration.RootPath = rootPath;
                    configuration.ThirdPartiesRootPath = thirdPartiesRootPath;
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
                    implementation = new ProjectFileModificationImplementation();
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
                Console.WriteLine(exc);
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
                    Name = "Visma.School.Admin",
                    Assemblies = new List<string>
                    {
                        "Visma.School.UI.Web",
                        "Visma.School.Services.Scheduler",
                        "Visma.School.UI.Web.Tests",
                        "Visma.School.Business.Services.IntegrationTests",
                        "Visma.School.Business.Services.Tests",
                        "DataAccess.Repositories.DataSeeder.Tests",
                        "DataAccess.Repositories.Readonly.Tests",
                    }
                },
                new ProjectInfo
                {
                    Name = "Visma.School.SelfService",
                    Assemblies = new List<string>
                    {
                        "Visma.School.SelfService.Api",
                        "Visma.School.SelfService.Api.Tests"
                    }
                },
                new ProjectInfo
                {
                    Name = "Visma.School.Integrations",
                    Assemblies = new List<string>
                    {
                        "Visma.School.Integrations.Api",
                        "Integrations.Api.Tests",
                        "IntegrationApi.TestClient"
                    }
                },
            };
            var data = JsonConvert.SerializeObject(config);
            Console.WriteLine(data);
        }

        private static void ShowHelp(string errorMessage)
        {
            Console.WriteLine(errorMessage);

            Console.WriteLine("-s - generate solution");
            Console.WriteLine("-f - fix project files");
            Console.WriteLine("-e - show example project file");
            Console.WriteLine("-p <project name> - which project to use from configuration");
            Console.WriteLine("-c <path> - path to configuration file");
            Console.WriteLine("-o <path> - solution output path");

            // -f -c "d:\Projects\VIGO\VismaSchool\master\Builds\solution configuration.json"
            // -s -p Visma.School.Admin -o ..\VismaSchool.SelfService.Gen.sln -c "d:\Projects\VIGO\VismaSchool\master\Builds\solution configuration.json"
            // -s -p Visma.School.AdminWithIntegrations -o ..\VismaSchool.Gen.sln -c "d:\Projects\VIGO\VismaSchool\master\Builds\solution configuration.json"
            // -s -p Visma.School.SelfService -o ..\VismaSchool.SelfService.Gen.sln -c "d:\Projects\VIGO\VismaSchool\master\Builds\solution configuration.json"
        }
    }
}
