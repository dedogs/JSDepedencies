using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace deDogs.JSDependencies
{
    public interface IJSDependencies
    {
        string Path { get; set; }
        bool SubDirectories { get; set; }
    }

    public class JSDependency : IJSDependencies
    {
        public JSDependency()
        {
            this.SubDirectories = true;
        }
        public string Path { get; set; }

        public bool SubDirectories { get; set; }
    }

    public class JSDependencies
    {
        private string rootPath;
        public string absolutePath = null;
        private readonly List<IJSDependencies> rootPaths;

        const string filePattern = @"(\/[\w\s-]+)";
        const string fileNamePattern = @"^.*\\([\w,\s-]+\.js)$";
        const string referencePattern = @"\/\/\/ <reference path=""(\.\.\/)*([\w]+\/)*([\w-.]+\.(js|ts))""\s?\/>";

        private static class ReferencePath
        {
            public static bool Parse(string search)
            {
                Regex rx = new Regex(regExpression, RegexOptions.IgnoreCase);
                Match matched = rx.Match(search);

                if (matched.Success)
                {

                    DirectoryNames = new List<string>();
                    foreach (Capture item in matched.Groups[2].Captures)
                    {
                        DirectoryNames.Add(item.Value);
                    }

                    NumberRelativePaths = matched.Groups[1].Captures.Count;
                    FileName = matched.Groups[3].Captures[0].Value.Replace("ts", "js");
                }
                else
                {
                    return false;
                }

                return true;
            }

            public static string regExpression { get; set; }
            public static int NumberRelativePaths { get; private set; }
            public static List<string> DirectoryNames { get; private set; }
            public static string FileName { get; private set; }

        }

        private string getRelativePath(string file)
        {
            return "~/" + file.Substring(file.IndexOf(this.rootPath)).Replace(@"\", "/");
        }

        private string mapPath(string path)
        {
            if (this.absolutePath == null)
            {
                path = HttpContext.Current.Server.MapPath(path);
            }
            else
            {
                path = this.absolutePath + path.Replace(@"~/", null).Replace("/", @"\");
            }
            return path;
        }

        public JSDependencies(IJSDependencies scriptPath)
            : this(new List<IJSDependencies> { scriptPath })
        {

        }

        public JSDependencies(IEnumerable<IJSDependencies> scriptPaths)
        {
            this.rootPaths = scriptPaths.ToList();
        }

        private string buildDirectory(MatchCollection folderList)
        {

            StringBuilder sb = new StringBuilder("~");

            for (int i = 0; i < folderList.Count - ReferencePath.NumberRelativePaths - 1; i++)
            {
                sb.Append(folderList[i].Value);
            }

            sb.Append("/");

            for (int i = 0; i < ReferencePath.DirectoryNames.Count; i++)
            {
                sb.Append(ReferencePath.DirectoryNames[i]);
            }

            return sb.ToString();
        }

        private List<string> getDependencies(string fullPath)
        {
            string file = mapPath(fullPath);
            ReferencePath.regExpression = referencePattern;
            List<string> references = new List<string>();
            string filePath;

            Regex has = new Regex(filePattern);
            MatchCollection folderList = has.Matches(fullPath);

            if (!File.Exists(file))
            {
                return references;
            }

            string relativeDirectory = "";

            //Read file contents. Reference tags are located at the beginning.
            foreach (var row in File.ReadLines(file))
            {
                //Row must match a reference directive. The directive must be position at the file's upper top row.
                //No empty rows between file's top row and a row containing a reference directive.
                if (!ReferencePath.Parse(row))
                {
                    break;
                }

                relativeDirectory = buildDirectory(folderList);

                filePath = String.Format("{0}{1}", relativeDirectory, ReferencePath.FileName ?? "");

                //Specified refernce may contain additional references.
                references.AddRange(getDependencies(filePath));
                references.Add(filePath);
            }

            return references;
        }

        //Locates all files within a specified directory.
        //All sub-directories are searched if includeSubDirectories is set true.
        private List<string> getFiles(string directory)
        {
            List<string> references = new List<string>();

            if (!Directory.Exists(directory))
            {
                return references;
            }

            string basePath, fullPath;
            string regularExpression = fileNamePattern;
            Regex a = new Regex(regularExpression);
            Match fileName;

            basePath = getRelativePath(directory);

            foreach (var file in Directory.GetFiles(directory))
            {
                fileName = a.Match(file);
                if (fileName.Success)
                {
                    fullPath = String.Format(@"{0}/{1}", basePath, fileName.Groups[1].Value);

                    references.AddRange(getDependencies(fullPath));

                    references.Add(fullPath);
                }
            }

            return references;
        }
        private string[] responses;

        public string[] include()
        {
            List<string> references = new List<string>();

            foreach (var item in this.rootPaths)
            {
                this.rootPath = item.Path.Replace("/", @"\");
                references.AddRange(include(item));
            }

            responses = references.Distinct().ToArray();
            return responses;

        }
        private string[] include(IJSDependencies current)
        {
            List<string> references = new List<string>();
            string[] responses;
            string currentPath = mapPath(current.Path);

            if (current.SubDirectories)
            {
                string[] directories = Directory.GetDirectories(currentPath);

                foreach (string directory in directories)
                {
                    references.AddRange(include(new JSDependency { Path = getRelativePath(directory) }));
                }
            }

            references.AddRange(getFiles(currentPath));
            responses = references.ToArray();

            return responses;
        }
    }
}