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
            if (action == FileActions.Delete || action == FileActions.Move)
            {
                Directory.Delete(source);
            }
        }

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

        public static void MirrorUpdate(string source, string mirror)
        {

            if (appearIdentical(source, mirror)) return;

            String[] ioData = Directory.GetFileSystemEntries(source);

            // Add missing folders and files
            foreach (string path in ioData)
            {
                if (File.Exists(path))
                {
                    // This path is a file
                    ProcessAddFile(path, source, mirror);
                }
                else if (Directory.Exists(path))
                {
                    // This path is a directory
                    ProcessAddDirectory(path, source, mirror);
                }
            }

            if (appearIdentical(source, mirror)) return;

            removeOrphans(source, mirror);
        }

        // Remove files and folders from the mirror that dont exist in the source folder
        private static void removeOrphans(string source, string mirror)
        {
            String[] ioData = Directory.GetFileSystemEntries(mirror);

            // Delete orphan folders and files
            foreach (string path in ioData)
            {
                if (File.Exists(path))
                {
                    if (!File.Exists(path.Replace(mirror, source))) File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    if (!Directory.Exists(path.Replace(mirror, source))) Directory.Delete(path, true);
                    else removeOrphans(path.Replace(mirror, source), path);
                }
            }
        }

        public static long GetDirectorySize(string path)
        {
            DirectoryInfo d = new DirectoryInfo(path);
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

        private static bool appearIdentical(string source, string mirror)
        {
            long sSize = GetDirectorySize(source);
            long mSize = GetDirectorySize(mirror);
            if (sSize == mSize) return true;
            return false;
        }

        private static void ProcessAddDirectory(string path, string source, string mirror)
        {
            string target = path.Replace(source, mirror);
            if (!Directory.Exists(target))
            {
                Directory.CreateDirectory(target);
                ProcessDirectoryRecursively(path, target, FileActions.Copy);
            }

            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(path);
            foreach (string fileName in fileEntries)
            {
                ProcessAddFile(fileName, source, mirror);
            }

            // Recurse into subdirectories of this directory.
            string[] subdirectoryEntries = Directory.GetDirectories(path);
            foreach (string subdirectory in subdirectoryEntries)
            {
                ProcessAddDirectory(subdirectory, source, mirror);
            }
        }

        private static void ProcessAddFile(string path, string source, string mirror)
        {
            string target = path.Replace(source, mirror);
            if (!File.Exists(target))
            {
                File.Copy(path, target);
            }
            else
            {
                FileInfo fS = new FileInfo(path);
                FileInfo fM = new FileInfo(target);
                if (fS.Length != fM.Length)
                {
                    File.Copy(path, target);
                }
            }
        }
    }
}
