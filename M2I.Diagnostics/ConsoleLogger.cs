using System;
using System.Collections.Generic;
using System.Text;

namespace M2I.Diagnostics
{
    /// <summary>
    /// Journal de type console.
    /// </summary>
    public class ConsoleLogger : Logger
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
                ConsoleColor color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(message);
                Console.ForegroundColor = color;
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
                ConsoleColor color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(message);
                Console.ForegroundColor = color;
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
                ConsoleColor color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(message);
                Console.ForegroundColor = color;
            }
        }

        #endregion
    }
}
