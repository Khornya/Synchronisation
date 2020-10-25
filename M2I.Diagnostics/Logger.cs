using System;

namespace M2I.Diagnostics
{
    /// <summary>
    /// Classe de base pour un journal de type <see cref="ILogger"/>.
    /// </summary>
    public abstract class Logger : ILogger
    {
        #region Properties

        /// <summary>
        /// Obtient le nom du journal.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Obtient la source du journal.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Obtient ou définit si les informations sont désactivées.
        /// </summary>
        public bool IsInformationBypassed { get; set; }

        /// <summary>
        /// Obtient ou définit si les avertissements sont désactivées.
        /// </summary>
        public bool IsWarningBypassed { get; set; }

        /// <summary>
        /// Obtient ou définit si les erreurs sont désactivées.
        /// </summary>
        public bool IsErrorBypassed { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Ecrit une information dans le journal.
        /// </summary>
        /// <param name="message">Information à écrire dans le journal.</param>
        public abstract void WriteInformation(string message);

        /// <summary>
        /// Ecrit un avertissement dans le journal.
        /// </summary>
        /// <param name="message">Avertissement à écrire dans le journal.</param>
        public abstract void WriteWarning(string message);

        /// <summary>
        /// Ecrit une erreur dans le journal.
        /// </summary>
        /// <param name="message">Erreur à écrire dans le journal.</param>
        public abstract void WriteError(string message);

        #endregion
    }
}
