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
                                if (!AreFilesIdentical(file, destFilepath))
                                {
                                    File.Copy(file, destFilepath, true);
                                }
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

        public static bool AreFilesIdentical(string file1, string file2)
        {
            OpenFilesAndWaitIfNeeded(file1, file2).ForEach(filestream => filestream?.Close());
            try
            {
                if (!File.Exists(file1) || !File.Exists(file2))
                {
                    return false;
                }
                byte[] file1Bytes;
                byte[] file2Bytes;
                try
                {
                    file1Bytes = File.ReadAllBytes(file1);
                    file2Bytes = File.ReadAllBytes(file2);
                } catch (Exception ex)
                {
                    Console.WriteLine($"Error while reading {file1} and {file2}");
                    throw ex;
                }
                if (file1Bytes.Length != file2Bytes.Length)
                {
                    return false;
                }
                else
                {
                    for (int i = 0; i < file1Bytes.Length; i++)
                    {
                        if (file1Bytes[i] == file2Bytes[i])
                        {
                            i++;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    return true;
                }
            } catch (Exception ex)
            {
                Console.WriteLine("Erreur :" + ex.Message);
                throw;
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
            bool isSourceFileBusy = !(string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath));
            bool isDestFileBusy = !(string.IsNullOrEmpty(destFilePath) || !File.Exists(destFilePath));
            bool wait = false;
            FileStream sourceFileStream = null;
            FileStream destFileStream = null;
            List<FileStream> fileStreams = new List<FileStream>();
            DateTime startDateTime = DateTime.Now;

            do
            {
                if (isSourceFileBusy)
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

                if (DateTime.Now > startDateTime.AddMinutes(1))
                {
                    throw new Exception("Délai d'attente dépassé, impossible d'ouvrir le(s) fichier(s).");
                }

            } while (isSourceFileBusy || isDestFileBusy);

            fileStreams.Add(sourceFileStream);
            fileStreams.Add(destFileStream);
            return fileStreams;
        }

        // Remove files and folders from the mirror that dont exist in the source folder
        public static void RemoveOrphans(string source, string destination)
        {
            String[] ioData = Directory.GetFileSystemEntries(destination);

            // Delete orphan folders and files
            foreach (string path in ioData)
            {
                if (File.Exists(path))
                {
                    if (!File.Exists(path.Replace(destination, source))) File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    if (!Directory.Exists(path.Replace(destination, source))) Directory.Delete(path, true);
                    else RemoveOrphans(path.Replace(destination, source), path);
                }
            }
        }        
    }
}
