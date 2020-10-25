using M2I.Diagnostics;
using M2I.Diagnostics.EventLog;
using Synchronisation.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Synchronisation.Service
{
    public partial class Service1 : ServiceBase
    {
        #region Fields

        /// <summary>
        ///     Instance du service.
        /// </summary>
        private readonly FileSyncService _Service;

        #endregion

        #region Constructors

        /// <summary>
        ///     Initialise une nouvelle instance de la classe <see cref="Service1"/>.
        /// </summary>
        public Service1()
        {
            this.InitializeComponent();
            Loggers.AvaillableLoggers.Add(new FileLogger()
            {
                Source = ".\\logfile.log"
            });
            Loggers.AvaillableLoggers.Add(new EventLogger()
            {
                Name = "Synchronization Service",
                Source = "Synchronization Service"
            });
            this._Service = new FileSyncService(ConfigurationManager.AppSettings.Get("Folder1"), ConfigurationManager.AppSettings.Get("Folder2"), ConfigurationManager.AppSettings.Get("SyncMode"));
            //Permet d'accepter la mise en pause et la reprise du service.
            this.CanPauseAndContinue = true;
        }

        #endregion

        #region Methods

        protected override void OnStart(string[] args) => this._Service.Start();

        protected override void OnStop() => this._Service.Stop();

        protected override void OnPause() => this._Service.Pause();

        protected override void OnContinue() => this._Service.Continue();

        #endregion
    }
}
