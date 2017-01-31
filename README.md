# simple-unity-cloud-build-patcher
A simple CLI application that syncs with Unity Cloud Build.  This makes it easy to distribute the latest version of your application to people who may be uncomfortable working with version control.  This patcher is written in C# for Windows and supports Mac through the Mono runtime environment.  The patcher integrates with Unity Cloud Build, a free service provided by Unity.

# How To Run #
This requires Mono to run.  Open the project in Xamarin Studio, Visual Studio, or MonoDevelop and hit "Run."  It compiles to a .exe which can be run on Windows natively and on Mac if Mono is installed.  To run on a Mac, open a terminal and type "mono FILENAME."  Optionally, the patcher can be compiled to a native Mac .app, but this requires additional steps that I deemed unnecessary for the scope of this project.

# Purpose #
The patcher was created to make it easy for employees of American Reading Company to provide feedback on the latest build of the ARC Games, even if they are not directly working with the code.  Unity Cloud Build is hooked into the master branch of the game's git repo and a new build for Windows and OSX is built automatically every time a new commit is pushed.  Testers are expected to boot the game via the patcher rather than the game's executable itself.  The patcher will first check to see if a more recent build of the game is available, and if so, it downloads the file and replaces the old version.  The patcher automatically launches the game when this process is complete.

# Technologies and Libraries #
The patcher is built on the open-source Mono framework, which is a runtime environment that allows C#/.NET software to run on OSX.  Mono is what makes cross-platform development through packages such as Unity and Xamarin Studio possible.  Mono compiles the code to .exe.  To run this file on a Mac, install Mono and run the file from a terminal using 'mono filename.exe'.

Most of the libraries used by this package are a part of the Common Language Infrastructure (CLI) or .NET.  Additionally, SharpZipLib is included for unzipping downloaded archives.  ICSharpCode.SharpZipLib.dll MUST be included in the same directory as the patcher for it to run.  The Newtonsoft.Json library is included in the project but not used.  This is left over from a previous attempt to access the relevant information from the API by parsing it as JSON (more on this later.)

This project has been maintained using Xamarin Studio Community Edition and can be safely modified using Visual Studio or MonoDevelop.

# Process #
If a configuration file exists, the patcher does not ask for any input from the user.  All steps are completed automatically.  The patcher saves and loads its state to an XML file which must be included in the same directory as the executable.  This file stores the version of the downloaded copy, as retrieved from the Unity Cloud Build API, as well as project-specific IDs and keys.  This file MUST be configured manually to get this patcher to work with a given project.  Consult the Unity website to retrieve your orgID, projectID, and apiKey.  Upon launch, the patcher reads the version information from this file and compares it to the most recently compiled build on the Unity Cloud Build server.  If there is a newer version available, it is downloaded.  The file is then decompressed, the archive destroyed, and the old version is overwritten by the new version.  The XML file is updated with the new versioning information, and the latest version is launched.

This patcher downloads the entire file every time.  This makes this patcher a good choice for small projects (especially mobile ones.)

# Supported Platforms #
This patcher is currently only written to support Windows and Mac, both 32-bit.  It assumes that Unity Cloud Build is set up to create builds for these platforms.  It can be easily expanded to handle 64-bit versions of these operating systems as well as Linux and UWP.

# Required Information #
The config file must contain an organization ID, project ID, and API key.  These can all be found on Unity's website.  If no XML file exists, the user will be prompted to enter this information.  Otherwise, it can be modified at any time by editing settings.xml.

# Room For Improvement #
The patcher currently parses the API using messy regular expressions.  Very little information is needed from the API (a version number and a download URL), so this hacky solution works fine, but it is a very ugly way to address the issue.
