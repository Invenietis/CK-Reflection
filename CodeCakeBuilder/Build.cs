﻿using Cake.Common;
using Cake.Common.Solution;
using Cake.Common.IO;
using Cake.Common.Tools.MSBuild;
using Cake.Common.Tools.NuGet;
using Cake.Core;
using Cake.Common.Diagnostics;
using SimpleGitVersion;
using Code.Cake;
using Cake.Common.Build.AppVeyor;
using System;
using System.Linq;
using Cake.Common.Tools.SignTool;
using Cake.Core.Diagnostics;
using Cake.Common.Text;
using Cake.Common.Tools.NuGet.Push;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Cake.Common.Tools.NUnit;
using Cake.Common.Tools.DotNetCore;
using Cake.Core.IO;
using Cake.Common.Tools.DotNetCore.Pack;
using Cake.Common.Build;
using Cake.Common.Tools.XUnit;

namespace CodeCake
{
    /// <summary>
    /// Standard build "script".
    /// </summary>
    [AddPath( "CodeCakeBuilder/Tools" )]
    [AddPath( "packages/**/tools*" )]
    public class Build : CodeCakeHost
    {
        public Build()
        {
            const string solutionName = "CK-Reflection";
            const string solutionFileName = solutionName + ".sln";

            var releasesDir = Cake.Directory( "CodeCakeBuilder/Releases" );

            // We do not publish .Tests projects for this solution.
            var projectsToPublish = Cake.ParseSolution( solutionFileName )
                                        .Projects
                                        .Where( p => !(p is SolutionFolder) 
                                                     && p.Name != "CodeCakeBuilder"
                                                     && !p.Path.Segments.Contains( "Tests" ) );

            var jsonS = Cake.GetSimpleJsonSolution();
            SimpleRepositoryInfo gitInfo = jsonS.RepositoryInfo;
            // Configuration is either "Debug" or "Release".
            string configuration = null;

            Teardown( cake =>
            {
                if( gitInfo.IsValid )
                {
                    jsonS.RestoreProjectFiles();
                }
            });

            Task( "Check-Repository" )
                .Does( () =>
                {
                    if (!gitInfo.IsValid)
                    {
                        if (Cake.IsInteractiveMode()
                            && Cake.ReadInteractiveOption("Repository is not ready to be published. Proceed anyway?", 'Y', 'N') == 'Y')
                        {
                            Cake.Warning("GitInfo is not valid, but you choose to continue...");
                        }
                        else throw new Exception("Repository is not ready to be published.");
                    }
                    else jsonS.UpdateProjectFiles(useNuGetV2Version: true);

                    configuration = gitInfo.IsValidRelease && gitInfo.PreReleaseName.Length == 0 ? "Release" : "Debug";

                    Cake.Information( "Publishing {0} projects with version={1} and configuration={2}: {3}",
                        projectsToPublish.Count(),
                        gitInfo.SemVer,
                        configuration,
                        string.Join( ", ", projectsToPublish.Select( p => p.Name ) ) );
                } );

            Task( "Restore-NuGet-Packages" )
                .Does( () =>
                {
                    Cake.DotNetCoreRestore();
                } );

            Task( "Clean" )
                .IsDependentOn( "Check-Repository" )
                .Does( () =>
                {
                    Cake.CleanDirectories( "**/bin/" + configuration, d => !d.Path.Segments.Contains( "CodeCakeBuilder" ) );
                    Cake.CleanDirectories( "**/obj/" + configuration, d => !d.Path.Segments.Contains( "CodeCakeBuilder" ) );
                    Cake.CleanDirectories( releasesDir );
                    Cake.DeleteFiles( "Tests/**/TestResult.xml" );
                } );

            Task( "Build" )
                .IsDependentOn( "Clean" )
                .IsDependentOn( "Restore-NuGet-Packages" )
                .IsDependentOn( "Check-Repository" )
                .Does( () =>
                {
                    using( var tempSln = Cake.CreateTemporarySolutionFile( solutionFileName ) )
                    {
                        tempSln.ExcludeProjectsFromBuild( "CodeCakeBuilder" );
                        Cake.MSBuild( tempSln.FullPath, settings =>
                        {
                            settings.Configuration = configuration;
                            settings.Verbosity = Verbosity.Normal;
                        } );
                    }
                } );

            Task( "Unit-Testing" )
                .IsDependentOn( "Build" )
                .Does( () =>
                {
                    Cake.CreateDirectory( releasesDir );
                    var testProjects = Cake.ParseSolution( solutionFileName )
                     .Projects
                         .Where( p => p.Name.EndsWith( ".Tests" ) )
                         .Select( p => p.Path );

                    Cake.XUnit2(testProjects);

                    //foreach (var test in testProjects)
                    //{
                    //    using (Cake.Environment.SetWorkingDirectory(test.GetDirectory()))
                    //    {
                    //        Cake.Information("Testing: {0}", test);
                    //    }
                    //}
                } );

            Task( "Create-NuGet-Packages" )
                .IsDependentOn( "Unit-Testing" )
                .WithCriteria( () => gitInfo.IsValid )
                .Does( () =>
                {
                    Cake.CreateDirectory( releasesDir );
                    foreach( SolutionProject p in projectsToPublish )
                    {
                        Cake.Warning(p.Path.GetDirectory().FullPath);
                        Cake.DotNetCorePack(p.Path.GetDirectory().FullPath, new DotNetCorePackSettings()
                        {
                            NoBuild = true,
                            OutputDirectory = releasesDir,
                            Verbose = true                        
                        });
                    }
                } );


            Task( "Push-NuGet-Packages" )
                .IsDependentOn( "Create-NuGet-Packages" )
                .WithCriteria( () => gitInfo.IsValid )
                .Does( () =>
                {
                    if (Cake.AppVeyor().IsRunningOnAppVeyor)
                    {
                        foreach (var file in Cake.GetFiles(releasesDir.Path + "/**/*"))
                            Cake.AppVeyor().UploadArtifact(file.FullPath);
                    }
                    IEnumerable<FilePath> nugetPackages = Cake.GetFiles( releasesDir.Path + "/*.nupkg" );
                    if( Cake.IsInteractiveMode() )
                    {
                        var localFeed = Cake.FindDirectoryAbove( "LocalFeed" );
                        if( localFeed != null )
                        {
                            Cake.Information( "LocalFeed directory found: {0}", localFeed );
                            if( Cake.ReadInteractiveOption( "Do you want to publish to LocalFeed?", 'Y', 'N' ) == 'Y' )
                            {
                                Cake.CopyFiles( nugetPackages, localFeed );
                            }
                        }
                    }
                    if( gitInfo.IsValidRelease )
                    {
                        if( gitInfo.PreReleaseName == "" 
                            || gitInfo.PreReleaseName == "prerelease" 
                            || gitInfo.PreReleaseName == "rc" )
                        {
                            PushNuGetPackages( "NUGET_API_KEY", "https://www.nuget.org/api/v2/package", nugetPackages );
                        }
                        else
                        {
                            // An alpha, beta, delta, epsilon, gamma, kappa goes to invenietis-preview.
                            PushNuGetPackages( "MYGET_PREVIEW_API_KEY", "https://www.myget.org/F/invenietis-preview/api/v2/package", nugetPackages );
                        }
                    }
                    else
                    {
                        Debug.Assert( gitInfo.IsValidCIBuild );
                        PushNuGetPackages( "MYGET_CI_API_KEY", "https://www.myget.org/F/invenietis-ci/api/v2/package", nugetPackages );
                    }
                } );

            // The Default task for this script can be set here.
            Task( "Default" )
                .IsDependentOn( "Push-NuGet-Packages" );

        }

        private void PushNuGetPackages( string apiKeyName, string pushUrl, IEnumerable<FilePath> nugetPackages )
        {
            // Resolves the API key.
            var apiKey = Cake.InteractiveEnvironmentVariable( apiKeyName );
            if( string.IsNullOrEmpty( apiKey ) )
            {
                Cake.Information( "Could not resolve {0}. Push to {1} is skipped.", apiKeyName, pushUrl );
            }
            else
            {
                var settings = new NuGetPushSettings
                {
                    Source = pushUrl,
                    ApiKey = apiKey,
                    Verbosity = NuGetVerbosity.Detailed
                };

                foreach( var nupkg in nugetPackages )
                {
                    Cake.Information($"Pushing '{nupkg}' to '{pushUrl}'.");
                    Cake.NuGetPush( nupkg, settings );
                }
            }
        }
    }
}