using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace M2I.Diagnostics
{
    /// <summary>
    /// Journal de type fichier.
    /// </summary>
    public class FileLogger : Logger
    {
        #region Methods

        private void WriteMessage(string message, string prefix)
        {
            File.AppendAllText(string.IsNullOrWhiteSpace(Source) ? ".\\log.txt" : Source, $"{prefix}[{DateTime.Now}] : {message}{Environment.NewLine}");
        }

        /// <summary>
        /// Ecrit une information dans le journal.
        /// </summary>
        /// <param name="message">Information à écrire dans le journal.</param>
        public override void WriteInformation(string message)
        {
            if (!IsInformationBypassed)
            {
                WriteMessage(message, "[I]");
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
                WriteMessage(message, "[W]");
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
                WriteMessage(message, "[E]");
            }
        }

        #endregion
    }
}
