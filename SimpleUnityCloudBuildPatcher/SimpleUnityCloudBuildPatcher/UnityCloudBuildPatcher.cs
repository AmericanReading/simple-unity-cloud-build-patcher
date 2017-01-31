// MIT License

// Copyright(c) 2017
// Bret Black

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib.Zip;

namespace SimpleUnityCloudBuildPatcher
{
	class UnityCloudBuildPatcher
	{
		WebRequest wrGETURL;
		int currentVersion;
		bool autoUpdate;
		string orgID, projectID, apiKey, clientUrl, unzippedUrl, os = "standaloneosxintel", buildTargetId = "_all", clientFolder = "game\\", settingsFile = "settings.xml";

		public static void Main(string[] args)
		{
			new UnityCloudBuildPatcher();
		}

		public UnityCloudBuildPatcher()
		{
			// get app settings
			if (!File.Exists(settingsFile))
			{
				GenerateSettingsFile(settingsFile);
			}

			// if there is a working internet connection, check for a new version
			if (CheckForInternetConnection())
			{
				ReadConfig();

				// get OS-specific information
				DetectOS();

				if (CheckForNewVersion(settingsFile, currentVersion))
				{
					// unzip if a new version has been downloaded.
					UnzipFile(clientUrl);
				}
			}

			// launch game
			LaunchClient(unzippedUrl);

			// exit client
			Environment.Exit(0);
		}

		/// <summary>
		/// Read important info from the config file.
		/// </summary>
		void ReadConfig()
		{
			XmlDocument doc = new XmlDocument();
			doc.Load(settingsFile);

			XmlNode node = doc.DocumentElement.SelectSingleNode("/settings/app");

			string greeting = node.SelectSingleNode("greetingText").InnerText;
			orgID = node.SelectSingleNode("orgID").InnerText;
			projectID = node.SelectSingleNode("projectID").InnerText;
			apiKey = node.SelectSingleNode("apiKey").InnerText;
			currentVersion = Int32.Parse(node.SelectSingleNode("version").InnerText);
			autoUpdate = Int32.Parse(node.SelectSingleNode("autoUpdate").InnerText) == 1;


			// print title
			Console.WriteLine(greeting);
		}

		/// <summary>
		/// Reads the OS, which determines how some file and directory-related things should be handled
		/// as well as the name of the target file.
		/// </summary>
		void DetectOS()
		{
			OperatingSystem osType = Environment.OSVersion;
			PlatformID pid = osType.Platform;

			switch (pid)
			{
				case PlatformID.Win32NT:
				case PlatformID.Win32S:
				case PlatformID.Win32Windows:
				case PlatformID.WinCE:
					os = "standalonewindows";
					unzippedUrl = "Default Windows desktop 32-bit.exe";
					break;
				case PlatformID.Unix:
					os = "standaloneosxintel";
					unzippedUrl = "Default Mac desktop 32-bit.app";
					break;
				default:
					Console.WriteLine("There was an issue detecting your operating system. Defaulting to Windows.");
					os = "standalonewindows";
					break;
			}
		}

		/// <summary>
		/// Reads the config file to get the currently installed version and compares it to the most recent
		/// cloud build.
		/// </summary>
		/// <returns><c>true</c>, if a new build is available, <c>false</c> otherwise.</returns>
		/// <param name="txtFileLocation">Config file URL.</param>
		/// <param name="currentVersion">Current version.</param>
		bool CheckForNewVersion(string txtFileLocation, int currentVersion)
		{
			// get all builds
			// send request
			wrGETURL = WebRequest.Create("https://build-api-builders.cloud.unity3d.com/api/v1/orgs/" + orgID + "/projects/" + projectID + "/buildtargets/" + buildTargetId + "/builds?buildStatus=success&platform=" + os);
			string username = apiKey;
			string password = "";
			string svcCredentials = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(username + ":" + password));
			wrGETURL.Headers.Add("Authorization", "Basic " + svcCredentials);

			// send
			Stream objStream = wrGETURL.GetResponse().GetResponseStream();

			StreamReader objReader = new StreamReader(objStream);
			string objString = objReader.ReadToEnd();

			// find URL
			string pattern = @"\d+";
			Regex rgx = new Regex(pattern);

			int newVersion = Convert.ToInt32(rgx.Match(objString).ToString());

			if (newVersion > currentVersion)
			{
				Console.WriteLine("New version found!");

				// ask the user if they want to update the file
				if (!autoUpdate)
				{
					bool confirmed = false;
					ConsoleKey response;
					do
					{
						Console.Write("Would you like to update your client? [Y/n] ");
						response = Console.ReadKey(false).Key;   // true is intercept key (dont show), false is show
						if (response != ConsoleKey.Enter)
							Console.WriteLine();

					} while (response != ConsoleKey.Y && response != ConsoleKey.N);

					confirmed = response == ConsoleKey.Y;

					// ignore the update, else fall through
					if (!confirmed)
					{
						return false;
					}
				}

				// get file
				GetFile(objString);

				// update text file
				SaveNewVersion(txtFileLocation, newVersion);

				return true;
			}
			return false;
		}

		/// <summary>
		/// Downloads the latest build of the game
		/// </summary>
		/// <param name="buildInfo">Build info, as retrieved from the Unity Cloud Build API.</param>
		void GetFile(string buildInfo)
		{
			//string url = location;
			Console.WriteLine("Connecting...");
			Console.WriteLine("Downloading...");

			// find URL
			string pattern = "(?<=\"download_primary\":{\"method\":\"get\",\"href\":\")(.*?)(?=\",\"meta\")";
			Regex rgx = new Regex(pattern);

			string match = rgx.Match(buildInfo).ToString();

			using (WebClient wc = new WebClient())
			{
				// print file size
				wc.OpenRead(match);
				Int64 fileSize = Convert.ToInt64(wc.ResponseHeaders["Content-Length"]);
				double fileSizeRounded = Math.Ceiling((fileSize / (1000000f)));
				Console.WriteLine("File Size: " + fileSizeRounded + "mb");
				Console.WriteLine("Downloading.  This may take a minute.");

				// get data
				byte[] data = wc.DownloadData(match);

				// get file name
				if (!String.IsNullOrEmpty(wc.ResponseHeaders["Content-Disposition"]))
				{
					clientUrl = wc.ResponseHeaders["Content-Disposition"].Substring(wc.ResponseHeaders["Content-Disposition"].IndexOf("filename=") + 10).Replace("\"", "");
					Console.WriteLine(clientUrl);
				}

				// write to file
				File.WriteAllBytes(clientUrl, data);
			}
			Console.WriteLine("Download Complete!");
		}

		/// <summary>
		/// Launches the game
		/// </summary>
		/// <param name="location">URL of the client.</param>
		void LaunchClient(string location)
		{
			string launchFrom = "";
			switch (os)
			{
				case "standalonewindows":
					launchFrom = clientFolder + location;
					break;

				case "standaloneosxintel":
				default:
					launchFrom = location;
					break;
			}

			// try to launch
			if (File.Exists(launchFrom) || Directory.Exists(launchFrom))
			{
				Console.WriteLine("Launching client!");
				System.Diagnostics.Process.Start(launchFrom);
			}
			else {
				Console.WriteLine("No client found.  Make sure you are connected to the internet and a Unity Cloud Build exists for this operating system.");
			}
		}

		/// <summary>
		/// Save the file name and version
		/// </summary>
		/// <param name="location">URL of the config file.</param>
		/// <param name="version">Current build version.</param>
		void SaveNewVersion(string location, int version)
		{
			try
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(settingsFile);

				XmlNode node = doc.DocumentElement.SelectSingleNode("/settings/app");
				node.SelectSingleNode("version").InnerText = version.ToString();
				doc.Save(settingsFile);
			}
			catch (Exception e)
			{
				Console.WriteLine("Something went wrong with saving the file!");
				Console.WriteLine(e);
			}
		}

		/// <summary>
		/// If no settings file exists, one is generated.
		/// </summary>
		/// <param name="loc">Location to save to.</param>
		void GenerateSettingsFile(string loc)
		{
			// query the user for input
			Console.WriteLine("No settings file found.  Generating a new one.  These settings can be changed at any time by modifying 'settings.xml'\n");
			Console.WriteLine("Enter a greeting:");
			string greeting = Console.ReadLine();
			currentVersion = -1;
			Console.WriteLine("Enter your organization ID:");
			orgID = Console.ReadLine();
			Console.WriteLine("Enter your project ID:");
			projectID = Console.ReadLine();
			Console.WriteLine("Enter your API key:");
			apiKey = Console.ReadLine();

			string autoUpdateRaw = "0";
			ConsoleKey response;
			do
			{
				Console.Write("Would you like files to automatically update? [Y/n] ");
				response = Console.ReadKey(false).Key;   // true is intercept key (dont show), false is show
				if (response != ConsoleKey.Enter)
					Console.WriteLine();

			} while (response != ConsoleKey.Y && response != ConsoleKey.N);

			if (response == ConsoleKey.Y)
			{
				autoUpdateRaw = "1";
			}

			autoUpdate = Int32.Parse(autoUpdateRaw) == 1;

			// save input to XML file
			XDocument doc = new XDocument(new XElement("settings",
													   new XElement("app",
																	new XElement("greetingText", greeting),
																	new XElement("autoUpdate", Int32.Parse(autoUpdateRaw)),
																	new XElement("version", currentVersion),
																	new XElement("orgID", orgID),
																	new XElement("projectID", projectID),
																	new XElement("apiKey", apiKey))));
			doc.Save(loc);
		}

		/// <summary>
		/// Unzips the downloaded client and deletes the archive.
		/// Note that Windows treats the game as a file (.exe) and mac an archive (.app)
		/// </summary>
		/// <param name="zipPath">URL of the zip folder.</param>
		void UnzipFile(string zipPath)
		{
			switch (os)
			{
				case "standaloneosxintel":

					// delete old file
					if (Directory.Exists(unzippedUrl))
					{
						Directory.Delete(unzippedUrl, true);
					}

					Process p = Process.Start(zipPath);
					p.WaitForExit();

					// wait until file is fully extracted
					int i = 0;
					while (!Directory.Exists(unzippedUrl) && i < 100)
					{
						System.Threading.Thread.Sleep(100);
						i++;
					}

					if (i >= 100)
					{
						Console.WriteLine("Unzipping timed out!");
					}

					break;

				case "standalonewindows":
				default:
					// delete old file
					if (Directory.Exists(clientFolder))
					{
						Directory.Delete(clientFolder, true);
					}

					if (!(File.Exists(zipPath)))
						return;

					try
					{
						FastZip unzip = new FastZip();
						unzip.ExtractZip(zipPath, clientFolder, "");
					}

					catch (Exception ex)
					{
						Console.WriteLine(ex);
						return;
					}

					break;
			}

			File.Delete(zipPath);
		}

		/// <summary>
		/// Checks for an internet connection
		/// </summary>
		/// <returns><c>true</c>, if for internet connection was found, <c>false</c> otherwise.</returns>
		bool CheckForInternetConnection()
		{
			try
			{
				using (var client = new WebClient())
				{
					using (var stream = client.OpenRead("http://www.google.com"))
					{
						return true;
					}
				}
			}
			catch
			{
				return false;
			}
		}
	}
}