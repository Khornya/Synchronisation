using System;
using System.Collections.Generic;
using System.Text;

namespace M2I.Diagnostics
{
    /// <summary>
    /// Aggrégateur de journaux. Permet d'écrire simultanément dans plusieurs journaux.
    /// </summary>
    public static class Loggers
    {
        #region Fields

        /// <summary>
        /// Journaux disponibles.
        /// </summary>
        private static List<ILogger> _AvaillableLoggers;

        #endregion

        #region Properties

        /// <summary>
        /// Obtient les journaux disponibles.
        /// </summary>
        public static List<ILogger> AvaillableLoggers => _AvaillableLoggers;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructeur statique de la classe <see cref="Loggers"/>.
        /// </summary>
        static Loggers()
        {
            _AvaillableLoggers = new List<ILogger>();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Ecrit une information dans les journaux disponibles.
        /// </summary>
        /// <param name="message">Message à écrire dans le journal.</param>
        public static void WriteInformation(string message) => _AvaillableLoggers.ForEach(l => l.WriteInformation(message));

        /// <summary>
        /// Ecrit un avertissement dans les journaux disponibles.
        /// </summary>
        /// <param name="message">Message à écrire dans le journal.</param>
        public static void WriteWarning(string message) => _AvaillableLoggers.ForEach(l => l.WriteWarning(message));

        /// <summary>
        /// Ecrit une erreur dans les journaux disponibles.
        /// </summary>
        /// <param name="message">Message à écrire dans le journal.</param>
        public static void WriteError(string message) => _AvaillableLoggers.ForEach(l => l.WriteError(message));

        #endregion
    }
}
