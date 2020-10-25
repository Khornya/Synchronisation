using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace M2I.Diagnostics
{
    /// <summary>
    /// Journal de type debug.
    /// </summary>
    public class DebugLogger : Logger
    {
        #region Methods

        /// <summary>
        /// Ecrit une information dans le journal.
        /// </summary>
        /// <param name="message">Information à écrire dans le journal.</param>
        public override void WriteInformation(string message)
        {
            if (!IsInformationBypassed)
            {
                Debug.WriteLine(message);
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
                Debug.WriteLine(message);
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
                Debug.WriteLine(message);
            }
        }

        #endregion
    }
}
