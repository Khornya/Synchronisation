using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Synchronisation.Core
{
    public class FileSyncService
    {
        //TODO: fix fuite mémoire
        //TODO: utiliser des Threads
        //TODO: 2-way sync
        //TODO: comparaison bit à bit des fichiers
        //TODO: sync à intervalle régulier
        //TODO: fichier de config
        //TODO: sync au démarrage

        #region Fields

        /// <summary>
        ///     Chemin du dossier d'entrée.
        /// </summary>
        private readonly string _InputFolderPath;

        /// <summary>
        ///     Chemin du dossier de sortie.
        /// </summary>
        private readonly string _OutputFolderPath;

        /// <summary>
        ///     Classe d'écoute du répertoire d'entrée.
        /// </summary>
        private FileSystemWatcher _Watcher;

        /// <summary>
        ///     Liste des événements.
        /// </summary>
        private List<FileSystemEventArgs> _Events;
        private Timer _Timer;
        private double _TimerInterval;

        #endregion

        #region Constructors

        /// <summary>
        ///     Initialise une nouvelle instance de la classe <see cref="FileSyncService"/>.
        /// </summary>
        /// <param name="inputFolderPath">Chemin du répertoire d'entrée.</param>
        /// <param name="outputFolderPath">Chemin du répertoire de sortie.</param>
        public FileSyncService(string inputFolderPath, string outputFolderPath, int millis)
        {
            this._InputFolderPath = !string.IsNullOrWhiteSpace(inputFolderPath) ?
                                        inputFolderPath : throw new ArgumentNullException(nameof(inputFolderPath));
            this._OutputFolderPath = !string.IsNullOrWhiteSpace(outputFolderPath) ?
                                        outputFolderPath : throw new ArgumentNullException(nameof(outputFolderPath));

            this._TimerInterval = millis;
        }

        #endregion

        #region Methods

        #region Service

        /// <summary>
        ///     Démarre le service.
        /// </summary>
        public void Start()
        {
            if (this._Watcher == null)
            {
                Console.WriteLine("Starting service...");

                try
                {
                    //Créé le dossier d'entrée s'il n'existe pas.
                    Directory.CreateDirectory(this._InputFolderPath);
                    Directory.CreateDirectory(this._OutputFolderPath);
                }
                catch (Exception ex)
                {
                    //En cas d'erreur, on log l'exception.
                    Console.WriteLine(ex.ToString());
                    //On relance l'exception pour arrêter le démarrage du service.
                    throw new Exception("Impossible de créer le dossier d'entrée ou de sortie", ex);
                }

                try
                {
                    //TODO: sync at start
                }
                catch (Exception)
                {

                    throw;
                }

                

                //On crée une instance d'un watcher sur le dossier d'entrée.
                this._Watcher = new FileSystemWatcher(this._InputFolderPath);
                //Permet de surveiller les sous-dossiers.
                this._Watcher.IncludeSubdirectories = true;
                //Permet de surveiller uniquement les changements dans les noms des fichiers et des dossiers, et les modifications
                this._Watcher.NotifyFilter = (NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName);
                //On s'abonne aux événements du Watcher.
                this._Watcher.Created += this.Watcher_Event;
                this._Watcher.Changed += this.Watcher_Event;
                this._Watcher.Deleted += this.Watcher_Event;
                this._Watcher.Renamed += this.Watcher_Event;
                this._Watcher.Error += this.Watcher_Error;

                //On démarre l'écoute.
                this._Watcher.EnableRaisingEvents = true;

                //On démarre le timer
                _Timer = new Timer();
                _Timer.Elapsed += this.Timer_Elapsed;
                _Timer.Interval = this._TimerInterval;
                _Timer.AutoReset = false;

                //On initialise la liste des événements
                this._Events = new List<FileSystemEventArgs>();

                Console.WriteLine("Service started.");
            }
        }

        /// <summary>
        ///     Met en pause l'exécution du service.
        /// </summary>
        public void Pause()
        {
            if (this._Watcher != null && this._Watcher.EnableRaisingEvents)
            {
                this._Watcher.EnableRaisingEvents = false;
                this._Events = new List<FileSystemEventArgs>();
                Console.WriteLine("Service paused.");
            }
        }

        /// <summary>
        ///     Reprend l'exécution du service.
        /// </summary>
        public void Continue()
        {
            if (this._Watcher != null && !this._Watcher.EnableRaisingEvents)
            {
                this._Watcher.EnableRaisingEvents = true;
                Console.WriteLine("Service resumed.");
            }
        }

        /// <summary>
        ///     Arrête l'exécution du service.
        /// </summary>
        public void Stop()
        {
            if (this._Watcher != null)
            {
                this._Watcher.Created -= this.Watcher_Event;
                this._Watcher.Changed -= this.Watcher_Event;
                this._Watcher.Deleted -= this.Watcher_Event;
                this._Watcher.Renamed -= this.Watcher_Event;
                this._Watcher.Error -= this.Watcher_Error;
                this._Watcher.Dispose();
                this._Watcher = null;
                this._Events = new List<FileSystemEventArgs>();
                Console.WriteLine("Service stopped");
            }
        }

        #endregion

        /// <summary>
        ///     Méthode déclenchée lors de l'ajout, de la modification ou de la suppression d'un fichier dans le répertoire d'entrée.
        /// </summary>
        /// <param name="sender">Instance qui a déclenché l'événement.</param>
        /// <param name="e">Arguments de l'événements.</param>
        private void Watcher_Event(object sender, FileSystemEventArgs e)
        {
            this._Timer.Start();
            this._Events.Add(e);
        }

        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            this._Timer.Start();
            Console.WriteLine($"[FileSystemWatcherError] {e.GetException()}");
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_Events.Count > 0)
            {
                List<FileSystemEventArgs> list = new List<FileSystemEventArgs>();
                lock (_Events)
                {
                    list.AddRange(_Events);
                    _Events.Clear();
                }

                process(list);
            }
        }

        private void process(List<FileSystemEventArgs> list)
        {
            string destination;
            List<FileSystemEventArgs> listF = new List<FileSystemEventArgs>();

            try
            {
                //--- Created ---//
                Console.WriteLine("----- Processing CREATED events -----");
                listF = list.Where(x => x.ChangeType == WatcherChangeTypes.Created).ToList();
                for (int i = 0; i < listF.Count; i++) //TODO: for each
                {
                    destination = listF[i].FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                    if (Directory.Exists(listF[i].FullPath))
                    {
                        Console.WriteLine($"Processing directory {listF[i].FullPath}");
                        Directory.CreateDirectory(destination);
                        FileUtils.ProcessDirectoryRecursively(listF[i].FullPath, destination, FileUtils.FileActions.Copy);
                        list.Remove(listF[i]);
                        // Remove all "Created" and "Changed" events of child folders and files from the master list
                        list = list.Where(s => (s.ChangeType == WatcherChangeTypes.Created || s.ChangeType == WatcherChangeTypes.Changed) && s.FullPath.Contains(listF[i].FullPath) == false).ToList();
                        // Update the sublist of "Created events"
                        listF = list.Where(x => x.ChangeType == WatcherChangeTypes.Created).ToList();
                        i -= 1;
                    }
                    else
                    {
                        Console.WriteLine($"Processing file {listF[i].FullPath}");
                        FileUtils.OpenFilesAndWaitIfNeeded(list[i].FullPath, destination).ForEach(filestream => filestream?.Close());
                        File.Copy(listF[i].FullPath, destination, true);
                        // Remove all "Changed" events for this file from the master list
                        list = list.Where(s => (s.ChangeType != WatcherChangeTypes.Changed && s.FullPath != listF[i].FullPath)).ToList();
                        // Update the sublist of "Created events"
                        listF = list.Where(x => x.ChangeType == WatcherChangeTypes.Created).ToList();
                        i -= 1;
                    }
                }
            }
            catch (Exception x)
            {
                Console.WriteLine($"[Error] Unable to process CREATED events {x.Message}");
            }
            try
            {
                //--- Changed ---//
                Console.WriteLine("----- Processing CHANGED events -----");
                listF = list.Where(z => z.ChangeType == WatcherChangeTypes.Changed).ToList();
                listF = RemoveDuplicates(listF);
                foreach (FileSystemEventArgs f in listF)
                {
                    if (File.Exists(f.FullPath))
                    {
                        Console.WriteLine($"Processing file {f.FullPath}");
                        destination = f.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                        FileUtils.OpenFilesAndWaitIfNeeded(f.FullPath, destination).ForEach(filestream => filestream?.Close());
                        File.Copy(f.FullPath, destination, true);
                    }

                }
            }
            catch (Exception x)
            {
                Console.WriteLine($"[Error] Unable to process CHANGED events {x.Message}");
            }
            try
            {
                //--- Renamed ---//
                Console.WriteLine("----- Processing RENAMED events -----");
                listF = list.Where(x => x.ChangeType == WatcherChangeTypes.Renamed).ToList();
                foreach (RenamedEventArgs f in listF)
                {
                    destination = f.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                    if (Directory.Exists(f.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath)))
                    {
                        string oldFPath = f.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath);
                        string newFPath = f.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                        Console.WriteLine($"Processing directory {oldFPath}");
                        FileUtils.ProcessDirectoryRecursively(oldFPath, newFPath, FileUtils.FileActions.Move);
                        Directory.Delete(oldFPath);
                    }
                    else
                    {
                        string oldFPath = f.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath);
                        string newFPath = f.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                        Console.WriteLine($"Processing file {oldFPath}");
                        FileUtils.OpenFilesAndWaitIfNeeded(oldFPath).ForEach(filestream => filestream?.Close());
                        File.Move(oldFPath, newFPath);
                    }
                }
            }
            catch (Exception x)
            {
                Console.WriteLine($"[Error] Unable to process RENAMED events {x.Message}");
            }
            try
            {
                //--- Deleted ---//
                Console.WriteLine("----- Processing DELETED events -----");
                listF = list.Where(x => x.ChangeType == WatcherChangeTypes.Deleted).ToList();
                foreach (FileSystemEventArgs f in listF)
                {
                    destination = f.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                    if (Directory.Exists(destination))
                    {
                        Console.WriteLine($"Processing directory {destination}");
                        FileUtils.ProcessDirectoryRecursively(destination, null, FileUtils.FileActions.Delete);
                        Directory.Delete(destination);
                    }
                    else
                    {
                        Console.WriteLine($"Processing file {destination}");
                        FileUtils.OpenFilesAndWaitIfNeeded(destination).ForEach(filestream => filestream?.Close());
                        File.Delete(destination);
                    }
                }
            }
            catch (Exception x)
            {
                Console.WriteLine($"[Error] Unable to process DELETED events {x.Message}");
            }
        }

        private List<FileSystemEventArgs> RemoveDuplicates(List<FileSystemEventArgs> list)
        {
            list = list.OrderBy(z => z.Name).OrderBy(z => z.FullPath).ToList();
            List<FileSystemEventArgs> newList = new List<FileSystemEventArgs>();
            String lastName = "";
            String lastFullPath = "+";
            foreach (FileSystemEventArgs fswa in list)
            {
                if (fswa.Name != lastName || fswa.FullPath != lastFullPath)
                {
                    newList.Add(fswa);
                    lastName = fswa.Name;
                    lastFullPath = fswa.FullPath;
                }
            }
            return newList;
        }

        #endregion
    }
}
