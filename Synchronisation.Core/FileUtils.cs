using System;
using System.Collections.Generic;
using System.IO;

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

        /// <summary>
        ///     Traite un dossier et son contenu.
        /// </summary>
        /// <param name="source">Le chemin du dossier source.</param>
        /// <param name="destination">Le chemin du dossier de destination.</param>
        /// <param name="action">L'action à effectuer.</param>
        public static void ProcessDirectoryRecursively(string source, string destination, FileActions action)
        {
            string[] files;

            if (action != FileActions.Delete)
            {
                if (destination?.Length > 0 && destination[destination.Length - 1] != Path.DirectorySeparatorChar)
                {
                    destination += Path.DirectorySeparatorChar;
                }
                if (!Directory.Exists(destination))
                {
                    try
                    {
                        Directory.CreateDirectory(destination);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error while creating directory", ex);
                    }
                }
            }

            files = Directory.GetFileSystemEntries(source);
            foreach (string file in files)
            {
                // Sub directories
                if (Directory.Exists(file))
                {
                    string newDestination = destination != null ? destination + Path.GetFileName(file) : null;
                    ProcessDirectoryRecursively(file, newDestination, action);
                }
                // Files in directory
                else
                {
                    string destFilepath = action == FileActions.Delete ? null : destination + Path.GetFileName(file);
                    if (action != FileActions.Delete || !File.Exists(destFilepath))
                    {
                        ProcessOneFile(file, destFilepath, action);
                    }
                }
            }
            if (action == FileActions.Delete || action == FileActions.Move)
            {
                try
                {
                    Directory.Delete(source);
                } catch (Exception ex)
                {
                    throw new Exception("Error while deleting directory", ex);
                }
            }
        }

        /// <summary>
        ///     Vérifie si deux fichiers sont identiques bit à bit.
        /// </summary>
        /// <param name="file1">Le chemin du premier fichier</param>
        /// <param name="file2">Le chemin du second fichier</param>
        /// <returns>true si les deux fichiers sont identiques, false sinon</returns>
        public static bool AreFilesIdentical(string file1, string file2)
        {
            OpenFilesAndWaitIfNeeded(file1, file2).ForEach(filestream => filestream?.Dispose());
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
                }
                catch (Exception ex)
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
            }
            catch (Exception ex)
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
                    sourceFileStream.Dispose();
                    destFileStream.Dispose();
                    throw new Exception("Délai d'attente dépassé, impossible d'ouvrir le(s) fichier(s).");
                }

            } while (isSourceFileBusy || isDestFileBusy);

            fileStreams.Add(sourceFileStream);
            fileStreams.Add(destFileStream);
            return fileStreams;
        }

        /// <summary>
        ///     Supprime les fichiers et sous-dossiers du dossier de destination qui n'existent pas dans le dossier source
        /// </summary>
        /// <param name="source">Le chemin du dossier source</param>
        /// <param name="destination">Le chemin du dossier de destination</param>
        public static void RemoveOrphans(string source, string destination)
        {
            String[] ioData = Directory.GetFileSystemEntries(destination);
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

        /// <summary>
        ///     Traite un fichier.
        /// </summary>
        /// <param name="source">Le chemin du fichier source.</param>
        /// <param name="destination">Le chemin du fichier de destination.</param>
        /// <param name="action">L'action à effectuer</param>
        internal static void ProcessOneFile(string source, string destination, FileActions action)
        {
            OpenFilesAndWaitIfNeeded(source, destination).ForEach(filestream => filestream?.Dispose());

            switch (action)
            {
                case FileActions.Copy:
                    if (!AreFilesIdentical(source, destination))
                    {
                        try
                        {
                            File.Copy(source, destination, true);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Error while copying file", ex);
                        }
                    }
                    break;
                case FileActions.Delete:
                    try
                    {
                        File.Delete(source);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error while deleting file", ex);
                    }
                    break;
                case FileActions.Move:
                    try
                    {
                        File.Move(source, destination);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error while moving file", ex);
                    }
                    break;
                default:
                    throw new ArgumentException();
            }
        }
    }
}
