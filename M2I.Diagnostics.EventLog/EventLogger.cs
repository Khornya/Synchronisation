using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace M2I.Diagnostics.EventLog
{
    /// <summary>
    /// Journal de type debug.
    /// </summary>
    public class EventLogger : Logger
    {
        #region Methods

        /// <summary>
        ///     Permet d'écrire un message dans le journal d'événement.
        /// </summary>
        /// <param name="entryType">Type d'entrée du message.</param>
        /// <param name="message">Message à écrire.</param>
        private void WriteMessage(EventLogEntryType entryType, string message)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new ArgumentNullException(nameof(Name), "Le nom du journal n'est pas précisé");
            }
            if (string.IsNullOrWhiteSpace(Source))
            {
                throw new ArgumentNullException(nameof(Source), "La source du journal n'est pas précisée");
            }

            if (!System.Diagnostics.EventLog.SourceExists(Source))
            {
                System.Diagnostics.EventLog.CreateEventSource(Source, Name);
            }

            System.Diagnostics.EventLog.WriteEntry(Source, message.Length > 30000 ? message.Substring(0, 30000) : message, entryType);
        }

        /// <summary>
        /// Ecrit une information dans le journal.
        /// </summary>
        /// <param name="message">Information à écrire dans le journal.</param>
        public override void WriteInformation(string message)
        {
            if (!IsInformationBypassed)
            {
                WriteMessage(EventLogEntryType.Information, message);
            }
        }

        /// <summary>
        /// Ecrit un avertissement dans le journal.
        /// </summary>
        /// <param name="message">Avertissement à écrire dans le journal.</param>
        public override void WriteWarning(string message)
        {
            if (!IsWarningBypassed)
            {
                WriteMessage(EventLogEntryType.Warning, message);
            }
        }

        /// <summary>
        /// Ecrit une erreur dans le journal.
        /// </summary>
        /// <param name="message">Erreur à écrire dans le journal.</param>
        public override void WriteError(string message)
        {
            if (!IsErrorBypassed)
            {
                WriteMessage(EventLogEntryType.Error, message);
            }
        }

        #endregion
    }
}
