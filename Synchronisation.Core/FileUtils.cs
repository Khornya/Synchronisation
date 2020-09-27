using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Synchronisation.Core
{
    public static class FileUtils
    {
        public enum FileActions
        {
            Copy,
            Delete,
            Move
        }

        public static long GetDirectorySize(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            return DirSize(di);
        }

        public static int GetFileCount(string path)
        {
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            int n = files.Length;
            return n;
        }

        public static int GetDirectoryCount(string path)
        {
            string[] dirs = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
            int n = dirs.Length;
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
        /// Processes files and directory structure recursively.
        ///</summary>
        public static void ProcessDirectoryRecursively(string source, string dest, FileActions action)
        {
            string[] files;
            
            if (action != FileActions.Delete)
            {
                if (dest?.Length > 0 && dest[dest.Length - 1] != Path.DirectorySeparatorChar)
                {
                    dest += Path.DirectorySeparatorChar;
                }
                if (!Directory.Exists(dest))
                {
                    Directory.CreateDirectory(dest);
                }
            }
                
            files = Directory.GetFileSystemEntries(source);
            foreach (string file in files)
            {
                // Sub directories
                if (Directory.Exists(file))
                {
                    string newDest = dest != null ? dest + Path.GetFileName(file) : null;
                    ProcessDirectoryRecursively(file, newDest, action);
                }
                // Files in directory
                else
                {
                    string destFilepath = action == FileActions.Delete ? null : dest + Path.GetFileName(file);
                    if (action != FileActions.Delete || !File.Exists(destFilepath))
                    {
                        OpenFilesAndWaitIfNeeded(file, destFilepath).ForEach(filestream => filestream?.Close());
                        switch (action)
                        {
                            case FileActions.Copy:
                                File.Copy(file, destFilepath, true);
                                break;
                            case FileActions.Delete:
                                File.Delete(file);
                                break;
                            case FileActions.Move:
                                File.Move(file, destFilepath);
                                break;
                            default:
                                throw new ArgumentException();
                        }
                    }
                }
            }
            if (action == FileActions.Delete)
            {
                Directory.Delete(source);
            }
        }

        //public static void DeleteAll(string path)
        //{
        //    if (Directory.Exists(path))
        //    {
        //        String[] Files;
        //        Files = Directory.GetFileSystemEntries(path);
        //        foreach (string element in Files)
        //        {
        //            if (Directory.Exists(element))
        //                Directory.Delete(element, true);
        //            else
        //            {
        //                OpenFilesAndWaitIfNeeded(element).ForEach(filestream => filestream?.Close());
        //                File.Delete(element);
        //            }
        //        }
        //    }
        //}

        /// <summary>
        ///     Ouvre un fichier avec attente si le fichier n'est pas disponible.
        /// </summary>
        /// <param name="sourceFilePath">Chemin du fichier à ouvrir.</param>
        /// <returns>Flux du fichier ouvert.</returns>
        public static List<FileStream> OpenFilesAndWaitIfNeeded(string sourceFilePath, string destFilePath = null)
        {
            //Console.WriteLine($"Trying to open {sourceFilePath} and {destFilePath}");
            bool isSourceFileBusy = true;
            bool isDestFileBusy = !(string.IsNullOrEmpty(destFilePath) || !File.Exists(destFilePath));
            bool wait = false;
            FileStream sourceFileStream = null;
            FileStream destFileStream = null;
            List<FileStream> fileStreams = new List<FileStream>();
            DateTime startDateTime = DateTime.Now;

            do
            {
                if(isSourceFileBusy)
                {
                    try
                    {
                        sourceFileStream = File.Open(sourceFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        isSourceFileBusy = false; //Si on arrive à ouvrir, le fichier est accessible
                        //Console.WriteLine($"Successfully opened {sourceFilePath}");
                    }
                    catch (IOException ex)
                    {
                        //Si on a une erreur d'IO, c'est que le fichier est encore ouvert
                        Console.WriteLine($"Unable to open {sourceFilePath}, retrying ...");
                        wait = true;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Erreur à l'ouverture du fichier", ex);
                    }

                }

                if (isDestFileBusy)
                {
                    try
                    {
                        destFileStream = File.Open(destFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        isDestFileBusy = false; //Si on arrive à ouvrir, le fichier est accessible
                        //Console.WriteLine($"Successfully opened {destFilePath}");
                    }
                    catch (IOException ex)
                    {
                        //Si on a une erreur d'IO, c'est que le fichier est encore ouvert
                        Console.WriteLine($"Unable to open {destFilePath}, retrying ...");
                        wait = true;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Erreur à l'ouverture du fichier", ex);
                    }
                }

                if (wait)
                {
                    System.Threading.Thread.Sleep(200);
                }

                if (DateTime.Now > startDateTime.AddMinutes(15))
                {
                    throw new Exception("Délai d'attente dépassé, impossible d'ouvrir le(s) fichier(s).");
                }

            } while (isSourceFileBusy || isDestFileBusy);

            fileStreams.Add(sourceFileStream);
            fileStreams.Add(destFileStream);
            return fileStreams;
        }
    }
}
