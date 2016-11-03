var json = "./project.json";

var target = Argument ("target", "Default");

Task ("Default").IsDependentOn ("build").IsDependentOn ("push");

Task ("build").IsDependentOn ("clean").Does (() => 
{
	DotNetCoreRestore();
	
	// Always use Jenkins configuration never Debug or Release they will bump version numbers.
	var buildSettings = new DotNetCoreBuildSettings
	{
		Configuration = "Jenkins"
	};
	
	DotNetCoreBuild(json, buildSettings);
	
	// Create packing settings for .NET core.
	var packSettings = new DotNetCorePackSettings {
		Configuration = "Jenkins",
		OutputDirectory = "nupkg",
		Verbose = true,
		NoBuild = true
	};
            
	DotNetCorePack(json, packSettings);
});

Task ("push").Does (() =>
{
	// Get the newest (by last write time) to publish
	var newestNupkg = GetFiles ("nupkg/*.nupkg")
		.OrderBy (f => new System.IO.FileInfo (f.FullPath).LastWriteTimeUtc)
		.LastOrDefault ();

	var apiKey = TransformTextFile ("./.nugetapi.key").ToString();
	var nugetSettings = new NuGetPushSettings { 
		Verbosity = NuGetVerbosity.Detailed,
		ApiKey = apiKey,
		Source = "https://www.nuget.org/api/v2/package"
	};
	
	NuGetPush (newestNupkg, nugetSettings);
});

Task ("clean").Does (() => 
{
	CleanDirectories ("./**/bin");
	CleanDirectories ("./**/obj");

	CleanDirectories ("./**/Components");
	//CleanDirectories ("./**/tools");

	DeleteFiles ("./**/*.apk");
});

RunTarget (target);