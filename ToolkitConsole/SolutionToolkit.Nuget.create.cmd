mkdir publish

.nuget\Nuget.exe pack SolutionGenerator.Console.csproj -o publish -Tool

pause