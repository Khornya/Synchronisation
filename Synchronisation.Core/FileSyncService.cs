using M2I.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Synchronisation.Core
{
    public class FileSyncService
    {
        //TODO: loggers
        //TODO: commentaires
        //TODO: vérifier fonctionnement du service
        //TODO : éviter startswith et replace

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
        private FileSystemWatcher _InputWatcher;

        /// <summary>
        ///     Liste des événements.
        /// </summary>
        private List<Change> _Events;

        private List<string> _IgnoredFolders;
        private List<string> _IgnoredFiles;
        private Thread _Worker;
        private FileSystemWatcher _OutputWatcher;
        private volatile bool _ShouldInterrupt;
        private bool _Interrupted;

        #endregion

        #region Constructors

        /// <summary>
        ///     Initialise une nouvelle instance de la classe <see cref="FileSyncService"/>.
        /// </summary>
        /// <param name="folder1Path">Chemin du répertoire d'entrée.</param>
        /// <param name="folder2Path">Chemin du répertoire de sortie.</param>
        public FileSyncService(string folder1Path, string folder2Path, string syncMode)
        {
            this._SyncMode = (SyncMode)Enum.Parse(typeof(SyncMode), syncMode);
            if (string.IsNullOrWhiteSpace(folder1Path))
            {
                throw new ArgumentNullException(nameof(folder1Path));
            }
            if (string.IsNullOrWhiteSpace(folder2Path))
            {
                throw new ArgumentNullException(nameof(folder2Path));
            }
            this._InputFolderPath = this._SyncMode == SyncMode.TwoWayDestFirst ? folder2Path : folder1Path;
            this._OutputFolderPath = this._SyncMode == SyncMode.TwoWayDestFirst ? folder1Path : folder2Path;
        }

        #endregion

        #region Methods

        #region Service

        /// <summary>
        ///     Démarre le service.
        /// </summary>
        public void Start()
        {
            if (this._InputWatcher == null)
            {
                Loggers.WriteInformation("Starting service...");

                try
                {
                    //Créé le dossier d'entrée s'il n'existe pas.
                    Directory.CreateDirectory(this._InputFolderPath);
                    Directory.CreateDirectory(this._OutputFolderPath);
                }
                catch (Exception ex)
                {
                    //En cas d'erreur, on log l'exception.
                    Loggers.WriteError(ex.ToString());
                    //On relance l'exception pour arrêter le démarrage du service.
                    throw new Exception("Impossible de créer le dossier d'entrée ou de sortie", ex);
                }

                try
                {
                    // On copie les fichiers du dossier prioritaire dans le dossier non prioritaire
                    FileUtils.ProcessDirectoryRecursively(this._InputFolderPath, this._OutputFolderPath, FileUtils.FileActions.Copy);
                    if (_SyncMode == SyncMode.TwoWaySourceFirst || _SyncMode == SyncMode.TwoWayDestFirst)
                    {
                        // On copie les fichiers présents uniquement dans le dossier non prioritaire
                        FileUtils.ProcessDirectoryRecursively(this._OutputFolderPath, this._InputFolderPath, FileUtils.FileActions.Copy);
                    }
                    if (_SyncMode == SyncMode.OneWay)
                    {
                        // On supprime les fichiers présents uniquement dans le dossier non prioritaire
                        FileUtils.RemoveOrphans(this._InputFolderPath, this._OutputFolderPath);
                    }
                }
                catch (Exception ex)
                {
                    Loggers.WriteError(ex.Message);
                    throw new Exception("Unable to sync directories", ex);
                }

                //On initialise la liste des événements
                this._Events = new List<Change>();
                this._IgnoredFolders = new List<string>();
                this._IgnoredFiles = new List<string>();
                this._ShouldInterrupt = false;

                //On crée un thread pour traiter les événements
                this._Worker = new Thread(processEvents);
                this._Worker.Start();

                //On crée une instance d'un watcher sur le dossier d'entrée.
                this._InputWatcher = new FileSystemWatcher(this._InputFolderPath);
                //On augmente la taille du buffer pour récupérer un maximum d'événements.
                this._InputWatcher.InternalBufferSize = 64 * 1024;
                //Permet de surveiller les sous-dossiers.
                this._InputWatcher.IncludeSubdirectories = true;
                //Permet de surveiller uniquement les changements dans les noms des fichiers et des dossiers, et les modifications
                this._InputWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName;
                //On s'abonne aux événements du Watcher.
                this._InputWatcher.Created += this.Watcher_Event;
                this._InputWatcher.Changed += this.Watcher_Event;
                this._InputWatcher.Deleted += this.Watcher_Event;
                this._InputWatcher.Renamed += this.Watcher_Event;
                this._InputWatcher.Error += this.Watcher_Error;

                //On démarre l'écoute.
                this._InputWatcher.EnableRaisingEvents = true;

                if (this._SyncMode != SyncMode.OneWay)
                {
                    //On crée une instance d'un watcher sur le dossier de sortie.
                    this._OutputWatcher = new FileSystemWatcher(this._OutputFolderPath);
                    //On augmente la taille du buffer pour récupérer un maximum d'événements.
                    this._OutputWatcher.InternalBufferSize = 64 * 1024;
                    //Permet de surveiller les sous-dossiers.
                    this._OutputWatcher.IncludeSubdirectories = true;
                    //Permet de surveiller uniquement les changements dans les noms des fichiers et des dossiers, et les modifications
                    this._OutputWatcher.NotifyFilter = (NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName);
                    //On s'abonne aux événements du Watcher.
                    this._OutputWatcher.Created += this.Watcher_Event;
                    this._OutputWatcher.Changed += this.Watcher_Event;
                    this._OutputWatcher.Deleted += this.Watcher_Event;
                    this._OutputWatcher.Renamed += this.Watcher_Event;
                    this._OutputWatcher.Error += this.Watcher_Error;

                    //On démarre l'écoute.
                    this._OutputWatcher.EnableRaisingEvents = true;
                }

                Loggers.WriteInformation("Service started.");
            }
        }

        /// <summary>
        ///     Met en pause l'exécution du service.
        /// </summary>
        public void Pause()
        {
            this._ShouldInterrupt = true;
            while (!_Interrupted)
            {
                Thread.Sleep(200);
            }
            SetWatcherEvents(_InputWatcher, value: false);
            SetWatcherEvents(_OutputWatcher, value: false);
            this._Events = new List<Change>();
            this._IgnoredFolders = new List<string>();
            this._IgnoredFiles = new List<string>();
            Loggers.WriteInformation("Service paused.");
        }

        /// <summary>
        ///     Reprend l'exécution du service.
        /// </summary>
        public void Continue()
        {
            this._ShouldInterrupt = false;
            SetWatcherEvents(_InputWatcher, value: true);
            SetWatcherEvents(_OutputWatcher, value: true);
            Loggers.WriteInformation("Service resumed.");
        }

        /// <summary>
        ///     Arrête l'exécution du service.
        /// </summary>
        public void Stop()
        {
            if (this._InputWatcher != null)
            {
                this._InputWatcher.Created -= this.Watcher_Event;
                this._InputWatcher.Changed -= this.Watcher_Event;
                this._InputWatcher.Deleted -= this.Watcher_Event;
                this._InputWatcher.Renamed -= this.Watcher_Event;
                this._InputWatcher.Error -= this.Watcher_Error;
                this._InputWatcher.Dispose();
            }
            if (this._OutputWatcher != null)
            {
                this._OutputWatcher.Created -= this.Watcher_Event;
                this._OutputWatcher.Changed -= this.Watcher_Event;
                this._OutputWatcher.Deleted -= this.Watcher_Event;
                this._OutputWatcher.Renamed -= this.Watcher_Event;
                this._OutputWatcher.Error -= this.Watcher_Error;
                this._OutputWatcher.Dispose();
            }
            this._ShouldInterrupt = true;
            while (!_Interrupted)
            {
                Thread.Sleep(200);
            }
            _Events = new List<Change>();
            _IgnoredFolders = new List<string>();
            Loggers.WriteInformation("Service stopped");
        }

        #endregion

        /// <summary>
        ///     Méthode déclenchée lors de l'ajout, de la modification ou de la suppression d'un fichier dans le répertoire d'entrée.
        /// </summary>
        /// <param name="sender">Instance qui a déclenché l'événement.</param>
        /// <param name="e">Arguments de l'événements.</param>
        private void Watcher_Event(object sender, FileSystemEventArgs e)
        {
            if (!_IgnoredFolders.Exists(path => e.FullPath.StartsWith(path)) && !_IgnoredFiles.Exists(path => e.FullPath == path))
            {
                lock (_Events)
                {
                    _Events.Add(new Change
                    {
                        ChangeType = e.ChangeType,
                        FullPath = e.FullPath,
                        Name = e.Name,
                        OldFullPath = e.ChangeType == WatcherChangeTypes.Renamed ? ((RenamedEventArgs)e).OldFullPath : null,
                        OldName = e.ChangeType == WatcherChangeTypes.Renamed ? ((RenamedEventArgs)e).OldName : null
                    });
                }
            }
        }

        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            Loggers.WriteInformation($"[FileSystemWatcherError] {e.GetException()}");
        }

        private void processEvents(object obj)
        {
            while (true)
            {
                if (_ShouldInterrupt)
                {
                    _Interrupted = true;
                    Thread.Sleep(1000);
                }
                else
                {
                    _Interrupted = false;
                    if (_Events.Count > 0)
                    {
                        Change e = _Events.First();
                        lock (this._Events)
                        {
                            _Events.RemoveAt(0);
                        }
                        if (e.RetryCount <= 3)
                        {
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
                                    }
                                    else
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
                            try
                            {
                                process(e);
                            }
                            catch (Exception ex)
                            {
                                Loggers.WriteError($"Erreur pendant le traitement de {e.FullPath} ({e.ChangeType}) : {ex.Message}");
                                e.RetryCount++;
                                lock (_Events)
                                {
                                    _Events.Add(e);
                                }
                            }
                            finally
                            {
                                _IgnoredFiles = new List<string>();
                                _IgnoredFolders = new List<string>();
                                _InputWatcher.EnableRaisingEvents = true;
                                if (_OutputWatcher != null)
                                {
                                    _OutputWatcher.EnableRaisingEvents = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void process(Change e)
        {
            string source;
            string destination;
            bool isOutputFolderEvent = e.FullPath.StartsWith(_OutputFolderPath);
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    source = e.FullPath;
                    destination = isOutputFolderEvent ? e.FullPath.Replace(_OutputFolderPath, _InputFolderPath) : e.FullPath.Replace(_InputFolderPath, _OutputFolderPath); // éviter replace
                    if (Directory.Exists(e.FullPath))
                    {
                        Loggers.WriteInformation($"Processing directory {source} (created)");
                        ProcessDirectory(e, source, destination, FileUtils.FileActions.Copy);
                    }
                    else
                    {
                        Loggers.WriteInformation($"Processing file {source} (created)");
                        ProcessFile(e, source, destination, FileUtils.FileActions.Copy);
                    }
                    break;
                case WatcherChangeTypes.Changed:
                    destination = isOutputFolderEvent ? e.FullPath : e.FullPath.Replace(_InputFolderPath, _OutputFolderPath); // éviter replace
                    source = isOutputFolderEvent ? e.FullPath.Replace(_OutputFolderPath, _InputFolderPath) : e.FullPath;
                    if (File.Exists(source))
                    {
                        if (isOutputFolderEvent)
                        {
                            Loggers.WriteInformation($"Processing file {destination} (changed), recovering from {source}");
                        }
                        else
                        {
                            Loggers.WriteInformation($"Processing file {source} (changed)");
                        }
                        ProcessFile(e, source, destination, FileUtils.FileActions.Copy);
                    }
                    break;
                case WatcherChangeTypes.Renamed:
                    source = isOutputFolderEvent ? e.FullPath : e.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath);
                    destination = isOutputFolderEvent ? e.OldFullPath : e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);

                    if (Directory.Exists(source))
                    {
                        if (isOutputFolderEvent)
                        {
                            Loggers.WriteInformation($"Processing directory {destination} (renamed), reverting");
                        }
                        else
                        {
                            Loggers.WriteInformation($"Processing directory {source} (renamed), renaming {destination}");
                        }
                        ProcessDirectory(e, source, destination, FileUtils.FileActions.Move);
                    }
                    else if (File.Exists(source))
                    {
                        if (isOutputFolderEvent)
                        {
                            Loggers.WriteInformation($"Processing file {destination} (renamed), reverting");
                        }
                        else
                        {
                            Loggers.WriteInformation($"Processing file {source} (renamed), renaming {destination}");
                        }
                        ProcessFile(e, source, destination, FileUtils.FileActions.Move);
                    }
                    break;
                case WatcherChangeTypes.Deleted:
                    source = e.FullPath.Replace(_OutputFolderPath, _InputFolderPath);
                    destination = isOutputFolderEvent ? e.FullPath : e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                    if (isOutputFolderEvent)
                    {
                        if (Directory.Exists(source))
                        {
                            Loggers.WriteInformation($"Processing directory {destination} (deleted), recovering from {source}");
                            ProcessDirectory(e, source, destination, FileUtils.FileActions.Copy);
                        }
                        else if (File.Exists(source))
                        {
                            Loggers.WriteInformation($"Processing file {destination} (deleted), recovering from {source}");
                            ProcessFile(e, source, destination, FileUtils.FileActions.Copy);
                        }
                    }
                    else
                    {
                        if (Directory.Exists(destination))
                        {
                            Loggers.WriteInformation($"Processing directory {source} (deleted), deleting {destination}");
                            ProcessDirectory(e, destination, null, FileUtils.FileActions.Delete);
                        }
                        else if (File.Exists(destination))
                        {
                            Loggers.WriteInformation($"Processing file {source} (deleted), deleting {destination}");
                            ProcessFile(e, destination, null, FileUtils.FileActions.Delete);
                        }
                    }
                    break;
                default:
                    Loggers.WriteError("Unsupported change type");
                    break;
            }
        }

        private void ProcessFile(Change e, string source, string destination, FileUtils.FileActions action)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                SetOtherWatcherEvents(value: false, isOutputFolderEvent: source.StartsWith(_OutputFolderPath));
            }
            else
            {
                SetWatcherEvents(_OutputWatcher, false);
            }

            if (destination != null)
            {
                try
                {
                    Directory.CreateDirectory(Directory.GetParent(destination).FullName);
                }
                catch (Exception ex)
                {
                    throw new Exception("Error while creating directory", ex);
                }
            }

            FileUtils.ProcessOneFile(source, destination, action);

            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                SetOtherWatcherEvents(value: true, isOutputFolderEvent: source.StartsWith(_OutputFolderPath));
            }
            else
            {
                SetWatcherEvents(_OutputWatcher, true);
            }
        }

        private void ProcessDirectory(Change e, string source, string destination, FileUtils.FileActions action)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                SetOtherWatcherEvents(value: false, isOutputFolderEvent: e.FullPath.StartsWith(_OutputFolderPath));
                try
                {
                    Directory.CreateDirectory(destination);
                }
                catch (Exception ex)
                {
                    throw new Exception("Error while creating directory", ex);
                }
            }
            else if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                _IgnoredFolders.Add(e.FullPath);
            }
            else
            {
                SetWatcherEvents(_OutputWatcher, false);
            }
            FileUtils.ProcessDirectoryRecursively(source, destination, action);
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                SetOtherWatcherEvents(value: true, isOutputFolderEvent: e.FullPath.StartsWith(_OutputFolderPath));
            }
            else if (e.ChangeType == WatcherChangeTypes.Deleted && e.FullPath.StartsWith(_InputFolderPath))
            {
                _IgnoredFolders.Remove(e.FullPath);
            }
            else
            {
                SetWatcherEvents(_OutputWatcher, true);
            }
        }

        private void SetOtherWatcherEvents(bool value, bool isOutputFolderEvent)
        {
            if (isOutputFolderEvent)
            {
                this._InputWatcher.EnableRaisingEvents = value;
            }
            else if (this._OutputWatcher != null)
            {
                this._OutputWatcher.EnableRaisingEvents = value;
            }
        }

        private List<Change> RemoveDuplicates(List<Change> list)
        {
            list = list.OrderBy(change => change.Name).OrderBy(change => change.FullPath).ToList();
            List<Change> newList = new List<Change>();
            string lastName = "";
            string lastFullPath = "+";
            foreach (Change change in list)
            {
                if (change.Name != lastName || change.FullPath != lastFullPath)
                {
                    newList.Add(change);
                    lastName = change.Name;
                    lastFullPath = change.FullPath;
                }
            }
            return newList;
        }

        private void SetWatcherEvents(FileSystemWatcher watcher, bool value)
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = value;
            }
        }

        #endregion
    }
}
