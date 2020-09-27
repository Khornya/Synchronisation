using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Synchronisation.Core
{
    /// <summary>
    /// Change information
    /// </summary>
    public class Change
    {
        /// <summary>
        /// Gets or sets the type of the change.
        /// </summary>
        /// <value>
        /// The type of the change.
        /// </value>
        public WatcherChangeTypes ChangeType { get; set; }

        /// <summary>
        /// Gets or sets the full path.
        /// </summary>
        /// <value>
        /// The full path.
        /// </value>
        public string FullPath { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the old full path.
        /// </summary>
        /// <value>
        /// The old full path.
        /// </value>
        public string OldFullPath { get; set; }

        /// <summary>
        /// Gets or sets the old name.
        /// </summary>
        /// <value>
        /// The old name.
        /// </value>
        public string OldName { get; set; }
    }
}
