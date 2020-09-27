using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Synchronisation.Core
{
    public class FileSyncService
    {
        //TODO: 2-way sync
        //TODO: sync au démarrage
        //TODO: interrompre le worker à la pause du service

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
        ///     Mode de synchronisation
        /// </summary>
        private readonly SyncMode _SyncMode;

        public enum SyncMode
        {
            OneWay,
            TwoWaySourceFirst,
            TwoWayDestFirst
        }

        /// <summary>
        ///     Classe d'écoute du répertoire d'entrée.
        /// </summary>
        private FileSystemWatcher _Watcher;

        /// <summary>
        ///     Liste des événements.
        /// </summary>
        private List<Change> _Events;

        private Thread _Worker;

        #endregion

        #region Constructors

        /// <summary>
        ///     Initialise une nouvelle instance de la classe <see cref="FileSyncService"/>.
        /// </summary>
        /// <param name="inputFolderPath">Chemin du répertoire d'entrée.</param>
        /// <param name="outputFolderPath">Chemin du répertoire de sortie.</param>
        public FileSyncService(string inputFolderPath, string outputFolderPath, string syncMode)
        {
            this._InputFolderPath = !string.IsNullOrWhiteSpace(inputFolderPath) ?
                                        inputFolderPath : throw new ArgumentNullException(nameof(inputFolderPath));
            this._OutputFolderPath = !string.IsNullOrWhiteSpace(outputFolderPath) ?
                                        outputFolderPath : throw new ArgumentNullException(nameof(outputFolderPath));
            this._SyncMode = (SyncMode) Enum.Parse(typeof(SyncMode), syncMode);
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

                //On initialise la liste des événements
                this._Events = new List<Change>();

                //On crée un thread pour traiter les événements
                this._Worker = new Thread(processEvents);
                this._Worker.Start();

                //On crée une instance d'un watcher sur le dossier d'entrée.
                this._Watcher = new FileSystemWatcher(this._InputFolderPath);
                //On augmente la taille du buffer pour récupérer un maximum d'événements.
                this._Watcher.InternalBufferSize = 64 * 1024;
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

                Console.WriteLine("Service started.");
            }
        }

        private void processEvents(object obj)
        {
            while (true)
            {
                if (_Events.Count > 0)
                {
                    Change e = _Events.First();
                    lock (this._Events)
                    {
                        _Events.RemoveAt(0);
                    }
                    switch (e.ChangeType)
                    {
                        case WatcherChangeTypes.Changed:
                            lock (_Events)
                            {
                                _Events = RemoveDuplicates(_Events);
                            }
                            break;
                        case WatcherChangeTypes.Created:
                            if (Directory.Exists(e.FullPath))
                            {
                                lock (_Events)
                                {
                                    // Remove all "Created" and "Changed" events of child folders and files from the master list
                                    _Events = _Events.Where(s => (s.ChangeType == WatcherChangeTypes.Created || s.ChangeType == WatcherChangeTypes.Changed) && s.FullPath.Contains(e.FullPath) == false).ToList();
                                }
                            } else
                            {
                                lock (_Events)
                                {
                                    // Remove all "Changed" events for this file from the master list
                                    _Events = _Events.Where(s => (s.ChangeType != WatcherChangeTypes.Changed && s.FullPath != e.FullPath)).ToList();
                                }
                            }
                            break;
                        default:
                            break;
                    }
                    process(e);
                }
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
                this._Events = new List<Change>();
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
                this._Events = new List<Change>();
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
            lock (this._Events)
            {
                this._Events.Add(new Change {
                    ChangeType = e.ChangeType,
                    FullPath = e.FullPath,
                    Name = e.Name,
                    OldFullPath = e.ChangeType == WatcherChangeTypes.Renamed ? ((RenamedEventArgs) e).OldFullPath : null,
                    OldName = e.ChangeType == WatcherChangeTypes.Renamed ? ((RenamedEventArgs)e).OldName : null
                });
            }
        }

        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            Console.WriteLine($"[FileSystemWatcherError] {e.GetException()}");
        }

        private void process(Change e)
        {
            string destination;
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    destination = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                    if (Directory.Exists(e.FullPath))
                    {
                        Console.WriteLine($"Processing directory {e.FullPath} (created)");
                        Directory.CreateDirectory(destination);
                        FileUtils.ProcessDirectoryRecursively(e.FullPath, destination, FileUtils.FileActions.Copy);
                    }
                    else
                    {
                        Console.WriteLine($"Processing file {e.FullPath} (created)");
                        FileUtils.OpenFilesAndWaitIfNeeded(e.FullPath, destination).ForEach(filestream => filestream?.Close());
                        Directory.CreateDirectory(Directory.GetParent(destination).FullName);
                        File.Copy(e.FullPath, destination, true);
                    }
                    break;
                case WatcherChangeTypes.Changed:
                    if (File.Exists(e.FullPath))
                    {
                        Console.WriteLine($"Processing file {e.FullPath} (changed)");
                        destination = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                        FileUtils.OpenFilesAndWaitIfNeeded(e.FullPath, destination).ForEach(filestream => filestream?.Close());
                        Directory.CreateDirectory(Directory.GetParent(destination).FullName);
                        File.Copy(e.FullPath, destination, true);
                    }
                    break;
                case WatcherChangeTypes.Renamed:
                        destination = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                        if (Directory.Exists(e.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath)))
                        {
                            string oldFPath = e.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath);
                            string newFPath = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                            Console.WriteLine($"Processing directory {oldFPath} (renamed)");
                            FileUtils.ProcessDirectoryRecursively(oldFPath, newFPath, FileUtils.FileActions.Move);
                            Directory.Delete(oldFPath);
                        }
                        else
                        {
                            string oldFPath = e.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath);
                            string newFPath = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                            Console.WriteLine($"Processing file {oldFPath} (renamed)");
                            FileUtils.OpenFilesAndWaitIfNeeded(oldFPath).ForEach(filestream => filestream?.Close());
                            Directory.CreateDirectory(Directory.GetParent(newFPath).FullName);
                            File.Move(oldFPath, newFPath);
                        }
                        break;
                case WatcherChangeTypes.Deleted:
                        destination = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                        if (Directory.Exists(destination))
                        {
                            Console.WriteLine($"Processing directory {destination} (deleted)");
                            FileUtils.ProcessDirectoryRecursively(destination, null, FileUtils.FileActions.Delete);
                        }
                        else
                        {
                            if (File.Exists(destination))
                            {
                                Console.WriteLine($"Processing file {destination} (deleted)");
                                FileUtils.OpenFilesAndWaitIfNeeded(destination).ForEach(filestream => filestream?.Close());
                                File.Delete(destination);
                            }
                        }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private List<Change> RemoveDuplicates(List<Change> list)
        {
            list = list.OrderBy(z => z.Name).OrderBy(z => z.FullPath).ToList();
            List<Change> newList = new List<Change>();
            String lastName = "";
            String lastFullPath = "+";
            foreach (Change fswa in list)
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
