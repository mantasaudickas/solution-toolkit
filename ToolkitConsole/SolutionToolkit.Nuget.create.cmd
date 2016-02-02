mkdir publish

.nuget\Nuget.exe pack SolutionGenerator.Console.csproj -IncludeReferencedProjects -o publish

pause