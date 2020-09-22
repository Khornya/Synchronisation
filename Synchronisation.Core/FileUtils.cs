using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Synchronisation.Core
{
    public static class FileUtils
    {
        public static long GetDirectorySize(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            return DirSize(di);
        }

        public static int GetFileCount(string path)
        {
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            int n = files.Length;
            //files = null;
            return n;
        }

        public static int GetDirectoryCount(string path)
        {
            string[] dirs = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
            int n = dirs.Length;
            //dirs = null;
            return n;
        }

        public static bool DirectoryExists(string path)
        {
            //if (path[path.Length - 1] != Path.DirectorySeparatorChar)
            //    path += Path.DirectorySeparatorChar;
            if (Directory.Exists(path)) return true;
            return false;
        }

        private static long DirSize(DirectoryInfo d)
        {
            long Size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                Size += fi.Length;
            }
            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                Size += DirSize(di);
            }
            return (Size);
        }

        ///<summary>
        /// Copy files and directory structure recursively.
        ///</summary>
        public static void CopyDirectoryRecursively(string source, string mirror)
        {
            String[] Files;

            if (mirror[mirror.Length - 1] != Path.DirectorySeparatorChar)
                mirror += Path.DirectorySeparatorChar;
            if (!Directory.Exists(mirror))
                Directory.CreateDirectory(mirror);

            Files = Directory.GetFileSystemEntries(source);
            foreach (string Element in Files)
            {
                // Sub directories
                if (Directory.Exists(Element))
                    CopyDirectoryRecursively(Element, mirror + Path.GetFileName(Element));
                // Files in directory
                else
                    File.Copy(Element, mirror + Path.GetFileName(Element), true);
            }
        }

        public static void DeleteAll(string path)
        {
            if (Directory.Exists(path))
            {
                String[] Files;
                Files = Directory.GetFileSystemEntries(path);
                foreach (string element in Files)
                {
                    if (Directory.Exists(element))
                        Directory.Delete(element, true);
                    else
                        File.Delete(element);
                }
            }
        }
    }
}
