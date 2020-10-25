using System;
using System.Collections.Generic;
using System.Text;

namespace M2I.Diagnostics
{
    /// <summary>
    /// Fournit un mécanisme pour l'écriture dans un journal.
    /// </summary>
    public interface ILogger
    {
        #region Properties

        /// <summary>
        /// Obtient le nom du journal.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Obtient la source du journal.
        /// </summary>
        string Source { get; set; }

        /// <summary>
        /// Obtient ou définit si les informations sont désactivées.
        /// </summary>
        bool IsInformationBypassed { get; set; }

        /// <summary>
        /// Obtient ou définit si les avertissements sont désactivées.
        /// </summary>
        bool IsWarningBypassed { get; set; }

        /// <summary>
        /// Obtient ou définit si les erreurs sont désactivées.
        /// </summary>
        bool IsErrorBypassed { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Ecrit une information dans le journal.
        /// </summary>
        /// <param name="message">Information à écrire dans le journal.</param>
        void WriteInformation(string message);

        /// <summary>
        /// Ecrit un avertissement dans le journal.
        /// </summary>
        /// <param name="message">Avertissement à écrire dans le journal.</param>
        void WriteWarning(string message);

        /// <summary>
        /// Ecrit une erreur dans le journal.
        /// </summary>
        /// <param name="message">Erreur à écrire dans le journal.</param>
        void WriteError(string message);

        #endregion
    }
}
