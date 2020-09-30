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
        //TODO: interrompre le worker à la pause du service
        //TODO: refacto
        //TODO: copier seulement si les fichiers sont différents

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

                FileUtils.MirrorUpdate(this._InputFolderPath, this._OutputFolderPath);

                //On initialise la liste des événements
                this._Events = new List<Change>();
                this._IgnoredFolders = new List<string>();
                this._IgnoredFiles = new List<string>();

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
                this._InputWatcher.NotifyFilter = (NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName);
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

                Console.WriteLine("Service started.");
            }
        }

        private void processEvents(object obj)
        { //TODO : disable events on the other watcher when processing
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
                    process(e);
                }
            }
        }

        /// <summary>
        ///     Met en pause l'exécution du service.
        /// </summary>
        public void Pause()
        {
            if (this._InputWatcher != null && this._InputWatcher.EnableRaisingEvents)
            {
                this._InputWatcher.EnableRaisingEvents = false;
                this._Events = new List<Change>();
                this._IgnoredFolders = new List<string>();
                Console.WriteLine("Service paused.");
            }
        }

        /// <summary>
        ///     Reprend l'exécution du service.
        /// </summary>
        public void Continue()
        {
            if (this._InputWatcher != null && !this._InputWatcher.EnableRaisingEvents)
            {
                this._InputWatcher.EnableRaisingEvents = true;
                Console.WriteLine("Service resumed.");
            }
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
                this._InputWatcher = null; // TODO: à revoir, le service n'est pas arrêté
                this._Events = new List<Change>();
                this._IgnoredFolders = new List<string>();
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
            if (!this._IgnoredFolders.Exists(path => e.FullPath.StartsWith(path)) && !this._IgnoredFiles.Exists(path => e.FullPath == path))
            {
                lock (this._Events)
                {
                    this._Events.Add(new Change
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
            Console.WriteLine($"[FileSystemWatcherError] {e.GetException()}");
        }

        private void process(Change e)
        {
            string source;
            string destination;
            Boolean isOutputFolderEvent = e.FullPath.StartsWith(_OutputFolderPath);
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    destination = isOutputFolderEvent ? e.FullPath.Replace(_OutputFolderPath, _InputFolderPath) : e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                    if (Directory.Exists(e.FullPath))
                        {
                            Console.WriteLine($"Processing directory {e.FullPath} (created)");
                            if (isOutputFolderEvent) {
                                this._InputWatcher.EnableRaisingEvents = false;
                            } else if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = false;
                            }
                            Directory.CreateDirectory(destination);
                            FileUtils.ProcessDirectoryRecursively(e.FullPath, destination, FileUtils.FileActions.Copy);
                            if (isOutputFolderEvent)
                            {
                                this._InputWatcher.EnableRaisingEvents = true;
                            }
                            else if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = true;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Processing file {e.FullPath} (created)");
                            FileUtils.OpenFilesAndWaitIfNeeded(e.FullPath, destination).ForEach(filestream => filestream?.Close());
                            if (isOutputFolderEvent)
                            {
                                this._InputWatcher.EnableRaisingEvents = false;
                            }
                            else if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = false;
                            }
                            Directory.CreateDirectory(Directory.GetParent(destination).FullName);
                            File.Copy(e.FullPath, destination, true);
                            if (isOutputFolderEvent)
                            {
                                this._InputWatcher.EnableRaisingEvents = true;
                            }
                            else if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = true;
                            }
                    }
                    break;
                case WatcherChangeTypes.Changed:
                    if (!isOutputFolderEvent)
                    {
                        if (File.Exists(e.FullPath))
                        {
                            Console.WriteLine($"Processing file {e.FullPath} (changed)");
                            destination = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                            FileUtils.OpenFilesAndWaitIfNeeded(e.FullPath, destination).ForEach(filestream => filestream?.Close());
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = false;
                            }
                            Directory.CreateDirectory(Directory.GetParent(destination).FullName);
                            File.Copy(e.FullPath, destination, true);
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = true;
                            }
                        }
                    } else
                    {
                        source = e.FullPath.Replace(_OutputFolderPath, _InputFolderPath);
                        if (File.Exists(source))
                        {
                            Console.WriteLine($"Processing file {e.FullPath} (changed), recovering from {source}");
                            FileUtils.OpenFilesAndWaitIfNeeded(source, e.FullPath).ForEach(filestream => filestream?.Close());
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = false;
                            }
                            File.Copy(source, e.FullPath, true);
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = true;
                            }
                        }
                    }
                    break;
                case WatcherChangeTypes.Renamed:
                    if (!isOutputFolderEvent)
                    {
                        destination = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                        if (Directory.Exists(e.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath)))
                        {
                            string oldFPath = e.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath);
                            string newFPath = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                            Console.WriteLine($"Processing directory {oldFPath} (renamed)");
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = false;
                            }
                            FileUtils.ProcessDirectoryRecursively(oldFPath, newFPath, FileUtils.FileActions.Move);
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = true;
                            }
                        }
                        else
                        {
                            string oldFPath = e.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath);
                            string newFPath = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                            Console.WriteLine($"Processing file {oldFPath} (renamed)");
                            FileUtils.OpenFilesAndWaitIfNeeded(oldFPath).ForEach(filestream => filestream?.Close());
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = false;
                            }
                            Directory.CreateDirectory(Directory.GetParent(newFPath).FullName);
                            File.Move(oldFPath, newFPath);
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = true;
                            }
                        }
                    } else
                    {
                        if (Directory.Exists(e.FullPath))
                        {
                            Console.WriteLine($"Processing directory {e.OldFullPath} (renamed), reverting");
                            this._OutputWatcher.EnableRaisingEvents = false;
                            FileUtils.ProcessDirectoryRecursively(e.FullPath, e.OldFullPath, FileUtils.FileActions.Move);
                            this._OutputWatcher.EnableRaisingEvents = true;
                        }
                        else
                        {
                            Console.WriteLine($"Processing file {e.OldFullPath} (renamed), reverting");
                            FileUtils.OpenFilesAndWaitIfNeeded(e.FullPath, e.OldFullPath).ForEach(filestream => filestream?.Close());
                            this._OutputWatcher.EnableRaisingEvents = false;
                            Directory.CreateDirectory(Directory.GetParent(e.OldFullPath).FullName);
                            File.Move(e.FullPath, e.OldFullPath);
                            this._OutputWatcher.EnableRaisingEvents = true;
                        }
                    }
                    break;
                case WatcherChangeTypes.Deleted:
                    if (!isOutputFolderEvent)
                    {
                        destination = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                        if (Directory.Exists(destination))
                        {
                            Console.WriteLine($"Processing directory {destination} (deleted)");
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = false;
                            }
                            FileUtils.ProcessDirectoryRecursively(destination, null, FileUtils.FileActions.Delete);
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = true;
                            }
                        }
                        else if (File.Exists(destination))
                        {
                            Console.WriteLine($"Processing file {destination} (deleted)");
                            FileUtils.OpenFilesAndWaitIfNeeded(destination).ForEach(filestream => filestream?.Close());
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = false;
                            }
                            File.Delete(destination);
                            if (this._OutputWatcher != null)
                            {
                                this._OutputWatcher.EnableRaisingEvents = true;
                            }
                        }
                    } else
                    {
                        source = e.FullPath.Replace(_OutputFolderPath, _InputFolderPath);
                        if (Directory.Exists(source))
                        {
                            Console.WriteLine($"Processing directory {e.FullPath} (deleted), recovering from {source}");
                            this._IgnoredFolders.Add(e.FullPath);
                            FileUtils.ProcessDirectoryRecursively(source, e.FullPath, FileUtils.FileActions.Copy);
                            this._IgnoredFolders.Remove(e.FullPath);
                        }
                        else
                        {
                            if (File.Exists(source))
                            {
                                Console.WriteLine($"Processing file {e.FullPath} (deleted), recovering from {source}");
                                FileUtils.OpenFilesAndWaitIfNeeded(source, e.FullPath).ForEach(filestream => filestream?.Close());
                                this._OutputWatcher.EnableRaisingEvents = false;
                                File.Copy(source, e.FullPath);
                                this._OutputWatcher.EnableRaisingEvents = true;
                            }
                        }
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void ProcessOutputFolderDeletedFile(Change e, string source)
        {
            Console.WriteLine($"Processing file {e.FullPath} (deleted), recovering from {source}");
            FileUtils.OpenFilesAndWaitIfNeeded(source, e.FullPath).ForEach(filestream => filestream?.Close());
            this._IgnoredFolders.Add(e.FullPath);
            File.Copy(source, e.FullPath);
            this._IgnoredFolders.Remove(e.FullPath);
        }

        private void ProcessOutputFolderDeletedDirectory(Change e, string source)
        {
            Console.WriteLine($"Processing directory {e.FullPath} (deleted), recovering from {source}");
            this._IgnoredFolders.Add(e.FullPath);
            FileUtils.ProcessDirectoryRecursively(source, e.FullPath, FileUtils.FileActions.Copy);
            this._IgnoredFolders.Remove(e.FullPath);
        }

        private void ProcessInputFolderDeletedFile(Change e, string destination)
        {
            Console.WriteLine($"Processing file {e.FullPath} (deleted)");
            FileUtils.OpenFilesAndWaitIfNeeded(destination).ForEach(filestream => filestream?.Close());
            this._IgnoredFiles.Add(destination);
            File.Delete(destination);
            this._IgnoredFiles.Remove(destination);
        }

        private void ProcessInputFolderDeletedDirectory(Change e, string destination)
        {
            Console.WriteLine($"Processing directory {e.FullPath} (deleted)");
            this._IgnoredFolders.Add(destination);
            FileUtils.ProcessDirectoryRecursively(destination, null, FileUtils.FileActions.Delete);
            this._IgnoredFolders.Remove(destination);
        }

        private void ProcessOutputFolderRenamedFile(Change e)
        {
            Console.WriteLine($"Processing file {e.OldFullPath} (renamed), reverting");
            FileUtils.OpenFilesAndWaitIfNeeded(e.FullPath, e.OldFullPath).ForEach(filestream => filestream?.Close());
            this._IgnoredFiles.Add(e.FullPath);
            this._IgnoredFiles.Add(e.OldFullPath);
            Directory.CreateDirectory(Directory.GetParent(e.OldFullPath).FullName);
            File.Move(e.FullPath, e.OldFullPath);
            this._IgnoredFiles.Remove(e.FullPath);
            this._IgnoredFiles.Remove(e.OldFullPath);
        }

        private void ProcessOutputFolderRenamedDirectory(Change e)
        {
            Console.WriteLine($"Processing directory {e.OldFullPath} (renamed), reverting");
            this._IgnoredFolders.Add(e.FullPath);
            FileUtils.ProcessDirectoryRecursively(e.FullPath, e.OldFullPath, FileUtils.FileActions.Move);
            this._IgnoredFolders.Remove(e.FullPath);
        }

        private void ProcessInputFolderRenamedFile(Change e, string destination)
        {
            string oldFPath = e.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath);
            string newFPath = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
            Console.WriteLine($"Processing file {e.OldFullPath} (renamed)");
            FileUtils.OpenFilesAndWaitIfNeeded(oldFPath).ForEach(filestream => filestream?.Close());
            this._IgnoredFiles.Add(oldFPath);
            this._IgnoredFiles.Add(newFPath);
            Directory.CreateDirectory(Directory.GetParent(newFPath).FullName);
            File.Move(oldFPath, newFPath);
            this._IgnoredFiles.Remove(oldFPath);
            this._IgnoredFiles.Remove(newFPath);
        }

        private void ProcessInputFolderRenamedDirectory(Change e, string destination)
        {
            string oldFPath = e.OldFullPath.Replace(_InputFolderPath, _OutputFolderPath);
            string newFPath = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
            Console.WriteLine($"Processing directory {e.OldFullPath} (renamed)");
            this._IgnoredFolders.Add(e.OldFullPath);
            FileUtils.ProcessDirectoryRecursively(oldFPath, newFPath, FileUtils.FileActions.Move);
            this._IgnoredFolders.Remove(e.OldFullPath);
        }

        private void ProcessOutputFolderChangedFile(Change e)
        {
            string source = e.FullPath.Replace(_OutputFolderPath, _InputFolderPath);
            if (File.Exists(source))
            {
                Console.WriteLine($"Processing file {e.FullPath} (changed), recovering from {source}");
                FileUtils.OpenFilesAndWaitIfNeeded(source, e.FullPath).ForEach(filestream => filestream?.Close());
                this._IgnoredFiles.Add(e.FullPath);
                File.Copy(source, e.FullPath, true);
                this._IgnoredFiles.Remove(e.FullPath);
            }
        }

        private void ProcessInputFolderChangedFile(Change e)
        {
            if (File.Exists(e.FullPath))
            {
                Console.WriteLine($"Processing file {e.FullPath} (changed)");
                string destination = e.FullPath.Replace(_InputFolderPath, _OutputFolderPath);
                FileUtils.OpenFilesAndWaitIfNeeded(e.FullPath, destination).ForEach(filestream => filestream?.Close());
                this._IgnoredFiles.Add(destination);
                this._IgnoredFolders.Add(Directory.GetParent(destination).FullName);
                Directory.CreateDirectory(Directory.GetParent(destination).FullName);
                File.Copy(e.FullPath, destination, true);
                this._IgnoredFiles.Remove(destination);
                this._IgnoredFolders.Remove(Directory.GetParent(destination).FullName);
            }
        }

        private void ProcessCreatedFile(Change e, string destination, bool isOutputFolderEvent)
        {
            Console.WriteLine($"Processing file {e.FullPath} (created)");
            FileUtils.OpenFilesAndWaitIfNeeded(e.FullPath, destination).ForEach(filestream => filestream?.Close());
            this._IgnoredFiles.Add(destination);
            Directory.CreateDirectory(Directory.GetParent(destination).FullName);
            File.Copy(e.FullPath, destination, true);
            this._IgnoredFiles.Remove(destination);
        }

        private void ProcessCreatedDirectory(Change e, string destination, bool isOutputFolderEvent)
        {
            Console.WriteLine($"Processing directory {e.FullPath} (created)");
            this._IgnoredFolders.Add(destination);
            Directory.CreateDirectory(destination);
            FileUtils.ProcessDirectoryRecursively(e.FullPath, destination, FileUtils.FileActions.Copy);
            this._IgnoredFolders.Remove(destination);
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
