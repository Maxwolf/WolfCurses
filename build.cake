var sln = "./WolfCurses.sln";
var nuspec = "./WolfCurses.nuspec";

var target = Argument ("target", "Default");

Task ("Default").IsDependentOn ("build").IsDependentOn ("push");

Task ("build").IsDependentOn ("clean").Does (() => 
{
	DotNetCoreRestore();
	DotNetBuild (sln);
	
	// Create packing settings for .NET core.
	var packSettings = new DotNetCorePackSettings {
		Configuration = "Release",
		OutputDirectory = "nupkg",
		Verbose = true,
		NoBuild = true,
		VersionSuffix = "ci-" + GetEnvironmentVariable("BUILD_NUMBER")
	};
            
	DotNetCorePack(sln, settings);
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