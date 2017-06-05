using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;

namespace CryPixivUpdater
{
    public static class Program
    {
        static List<ItemDownload> ItemsToDownload = null;
        const string UpdateFile = "http://www.mediafire.com/edit/xc77ll99xq4y11d/updatelist.txt";

        static string ExecutableVersion = null;
        static string ExecutablePath = "";
        static string CurrentDirectory = "";

        static void Main(string[] args)
        {
            ForegroundColor = ConsoleColor.Gray;
            CurrentDirectory = Directory.GetCurrentDirectory();

            // find executable file to be updated
            ExecutablePath = GetExecutablePath();
            if (ExecutablePath == null)
            {
                ForegroundColor = ConsoleColor.Red;
                WriteLine("Executable not found!");
                ReadLine();
                return;
            }

            // check for updates
            Write("Checking for updates... ");
            bool isUpdate = CheckForUpdate(ExecutableVersion) != null;
            if (isUpdate == false)
            {
                ForegroundColor = ConsoleColor.Red;
                WriteLine("NO UPDATE FOUND");
                ReadLine();
                return;
            }
            ForegroundColor = ConsoleColor.Green;
            WriteLine("UPDATE FOUND");
            ForegroundColor = ConsoleColor.Gray;
            WriteLine("\nPress any button to continue...\n");
            ReadLine();
           

            // download the update
            Write("Downloading update...");
            try
            {
                DownloadUpdate();
            }
            catch (Exception ex)
            {
                WriteLine("ERROR");
                ForegroundColor = ConsoleColor.Red;
                WriteLine(ex.Message);
                ReadLine();
                return;
            }
            ForegroundColor = ConsoleColor.Green;
            WriteLine("OK");
            ForegroundColor = ConsoleColor.Gray;

            // wait for process to exit
            Write("Waiting for any running processes to exit... ");
            try
            {
                WaitForProcessExit(Path.GetFileName(ExecutablePath).Replace(".exe", ""));
            }
            catch (Exception ex)
            {
                WriteLine("ERROR");
                ForegroundColor = ConsoleColor.Red;
                WriteLine(ex.Message);
                ReadLine();
                return;
            }
            ForegroundColor = ConsoleColor.Green;
            WriteLine("OK");
            ForegroundColor = ConsoleColor.Gray;

            // apply the update
            Write("Applying update...");

            try
            {
                ApplyUpdate(ItemsToDownload);
            }
            catch (Exception ex)
            {
                WriteLine("ERROR");
                ForegroundColor = ConsoleColor.Red;
                WriteLine(ex.Message);
                ReadLine();
                return;
            }
            ForegroundColor = ConsoleColor.Green;
            WriteLine("OK");
            ForegroundColor = ConsoleColor.Gray;

            WriteLine("Done");
            ReadLine();
        }

        static string GetExecutablePath()
        {
            var files = Directory.GetFiles(CurrentDirectory, "*.exe");
            foreach (var f in files)
            {
                var info = new FileInfo(f);
                var v = AssemblyName.GetAssemblyName(f);
                if (v.Name == "CryPixivClient")
                {
                    ExecutableVersion = GetVersionString(v.Version);
                    return f;
                }
            }

            return null;
        }
        static void WaitForProcessExit(string exename)
        {
            List<Process> prc = null;
            int count = 0;
            do
            {
                count = 0;
                prc = Process.GetProcesses().Where(x => x.ProcessName.ToLower().Contains(exename.ToLower())).ToList();
                foreach (var p in prc) if (p.MainModule.FileName == ExecutablePath) count++;
                if (count > 0) Thread.Sleep(200);
            } while (count != 0);
        }

        static void DownloadUpdate()
        {
            if (ItemsToDownload == null) throw new InvalidOperationException("Check for updates first!");
            List<ItemDownload> items = new List<ItemDownload>();

            using (var client = new WebClient())
            {
                foreach(var i in ItemsToDownload)
                {
                    var link = GetActualDownloadLink(i.Url);
                    var data = client.DownloadData(link);
                    i.Data = data;
                }
            }
        }

        static string GetActualDownloadLink(string mediafireLink)
        {
            using (var client = new WebClient())
            {
                var src = client.DownloadString(mediafireLink);

                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(src);

                var inputs = from input in htmlDoc.DocumentNode.Descendants("a")
                             where input.Attributes["class"] != null
                             where input.Attributes["class"].Value.ToLower().Contains("downloadbuttonad-startdownload")
                             select input;

                // get actual download link
                var dlink = inputs.First().Attributes["href"].Value;
                return dlink;
            }
        }

        static void ApplyUpdate(List<ItemDownload> itemsToBeApplied)
        {
            foreach (var i in itemsToBeApplied)
            {
                File.WriteAllBytes(Path.Combine(CurrentDirectory, i.Filename), i.Data);
            }
        }

        public static string GetVersionString(Version v) => $"v{v.Major}.{v.MajorRevision}.{v.Minor}.{v.MinorRevision}";
        public static string CheckForUpdate(string version = null)
        {
            // get actual download link
            var dlink = GetActualDownloadLink(UpdateFile);

            // get file contents
            var client = new WebClient();
            var src = client.DownloadString(dlink);
            var parameters = src.Split('\n').ToList();
            client.Dispose();

            // parse contents
            var ver = parameters[0];
            ItemsToDownload = new List<ItemDownload>();

            for (int i = 1; i < parameters.Count; i += 2)
            {
                var filename = parameters[i];
                var url = parameters[i + 1];
                ItemsToDownload.Add(new ItemDownload()
                {
                    Filename = filename,
                    Url = url
                });
            }

            // compare versions
            if (IsVersionGreater(ver.Replace("v", ""), version.Replace("v", ""))) return ver;
            else return null;
        }

        public static bool IsVersionGreater(string version, string thanThisVersion)
        {
            var ver1 = version.Split('.').Select(x => int.Parse(x)).ToList();
            var ver2 = thanThisVersion.Split('.').Select(x => int.Parse(x)).ToList();

            if (ver1[0] > ver2[0]) return true;
            else if (ver1[0] < ver2[0]) return false;
            else
            {
                if (ver1[1] > ver2[1]) return true;
                else if (ver1[1] < ver2[1]) return false;
                else
                {
                    if (ver1[2] > ver2[2]) return true;
                    else if (ver1[2] < ver2[2]) return false;
                    else
                    {
                        if (ver1[3] > ver2[3]) return true;
                        else return false;
                    }
                }
            }
        }
    }

    internal class ItemDownload
    {
        public string Url { get; set; }
        public string Filename { get; set; }
        public byte[] Data { get; set; }
    }
}
