#tool "nuget:?package=Microsoft.TestPlatform&version=15.7.0"
#tool "nuget:?package=OpenCover&version=4.6.519"
#tool "nuget:?package=ReportGenerator&version=4.0.9"
#tool "nuget:?package=GitVersion&version=3.6.5"

#load "build/paths.cake"

var packageVersion = "0.1.0";
///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////
var target = Argument("target", "Compile");
var configuration = Argument("configuration", "Debug");
var codeCoverageReportPath= Argument<FilePath>("CodeCoverageReportPath", "coverage.zip");

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////
Task("temp")
    .Does(()=>{
        var path = Context.Tools.Resolve("vstest.console.exe");
        Information(path);
    });

Task("Clean")
    .Does(()=>{
        foreach (var dir in GetDirectories("**/Debug"))
        {
            Information($"Cleaning: {dir}");
            CleanDirectory(dir);
        }
        CleanDirectory(Paths.ReportDirectory);
    });

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore(Paths.SolutionPath);
});

Task("Generic-Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(()=>{
        foreach(var file in GetFiles("*.sln"))
        {
            Information("Compiling: {0}", file);
            MSBuild(file.ToString(), settings => settings.SetConfiguration(configuration));
        }
    });
    
Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(()=>{
        MSBuild(Paths.SolutionPath.ToString(), settings => settings.SetConfiguration(configuration));
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(()=> {
        OpenCover(
            tool => tool.VSTest($"**/bin/{configuration}/*.Test.dll", new VSTestSettings
                {
                    Parallel = true,
                    TestAdapterPath = @"Exercise.Cake.Test\bin\Debug\"
                }),
            Paths.CodeCoverageResultFile,
            new OpenCoverSettings()
                .WithFilter("+[Exercise.*]*")
                .WithFilter("-[Exercise.*Test*]*")
        );
    });

Task("Report-Coverage")
    .IsDependentOn("Test")
    .Does(()=> {
        ReportGenerator(
            Paths.CodeCoverageResultFile,
            Paths.ReportDirectory,
            new ReportGeneratorSettings
            {
                ReportTypes = new [] { ReportGeneratorReportType.Html }
            }
        );

        Zip(
            Paths.ReportDirectory,
            MakeAbsolute(codeCoverageReportPath)
        );
    });

Task("Version")
    .Does(()=>{
        var version = GitVersion();
        Information($"Calculated SemVersion {version.SemVer}");

        packageVersion = version.NuGetVersion;

        Information($"Calculated package version {packageVersion}");
        if(!BuildSystem.IsLocalBuild)
        {
            GitVersion(new GitVersionSettings
            {
                OutputType = GitVersionOutput.BuildServer,
                UpdateAssemblyInfo = true
            });
        }
    });
//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .IsDependentOn("Test");

RunTarget(target); 