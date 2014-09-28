using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server.Resources.Tasks
{
    /// <summary>
    /// A task to prepare a resource for execution.
    /// </summary>
    public abstract class ResourceTask
    {
        /// <summary>
        /// When overridden by a derived class, probes whether or not this task needs to be executed on a resource.
        /// </summary>
        /// <param name="resource">The resource to probe.</param>
        /// <returns>A boolean value.</returns>
        public abstract bool NeedsExecutionFor(Resource resource);

        /// <summary>
        /// When overridden by a derived class, processes the task on a resource.
        /// </summary>
        /// <param name="resource">The resource to process the task on.</param>
        /// <returns>Whether or not the task succeeded.</returns>
        public abstract bool Process(Resource resource);

        /// <summary>
        /// Gets an identifier for this task.
        /// </summary>
        public string Id
        {
            get
            {
                return GetType().Name;
            }
        }

        /// <summary>
        /// Gets the task IDs this task depends on.
        /// </summary>
        public abstract IEnumerable<string> DependsOn { get; }
    }
}
