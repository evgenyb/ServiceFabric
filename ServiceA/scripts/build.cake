#tool "NUnit.ConsoleRunner"
#tool "NUnit.Extension.TeamCityEventListener"
#tool "nuget:?package=OctopusTools"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var apiKey = Argument("apiKey", "API-4UYDPMET0PF6EDSFSLN9NUJWH9I");
var nugetOctopusPushFeed = Argument("nugetOctopusPushFeed", "http://localhost:8085/nuget/packages");
var nugetPushFeed = Argument("nugetPushFeed", "c:\nuget");
var packageVersion = Argument("packageVersion", "1.3.0");
var sfPackageName = "ServiceA";

var solution = File("../ServiceA.sln");

Task("CI-Build")
	.IsDependentOn("Push-ServiceA");

Task("NuGet-Restore")
    .Description("Restoring NuGet packages")
    .Does(() =>
	{
		NuGetRestore(solution);
	});

Task("build")
	.IsDependentOn("NuGet-Restore")
	.Does(() => DotNetBuild(solution));

Task("Pack-ServiceA")
	.IsDependentOn("build")
	.Does(() => 
	{


		DotNetBuild("../ServiceFabric.ServiceA/ServiceFabric.ServiceA.sfproj", 
			settings => settings
				.SetConfiguration("Release")
				.WithTarget("Package"));

		CopyDirectory("../ServiceFabric.ServiceA/pkg/Release/", "./pkg/ServiceA/");		
		CopyDirectory("../ServiceFabric.ServiceA/PublishProfiles", "./pkg/ServiceA/PublishProfiles");
		CopyDirectory("../ServiceFabric.ServiceA/ApplicationParameters", "./pkg/ServiceA/ApplicationParameters");

		OctoPack("ServiceA.ServiceFacade", new OctopusPackSettings
		{
			BasePath = "./pkg/ServiceA/",
			OutFolder = "./out/",
			Version = packageVersion
		});		
	});

Task("Push-ServiceA")
  .IsDependentOn("Pack-ServiceA")
  .Does(() =>
  {
	NuGetPush(
        "./out/ServiceA.ServiceFacade." + packageVersion + ".nupkg",
        new NuGetPushSettings
        {
            Source = nugetOctopusPushFeed,
            ApiKey = apiKey
        });
  });
Task("default")
  .IsDependentOn("build");

RunTarget(target);