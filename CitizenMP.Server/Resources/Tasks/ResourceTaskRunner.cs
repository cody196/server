using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuickGraph;
using QuickGraph.Algorithms;

namespace CitizenMP.Server.Resources.Tasks
{
    class ResourceTaskRunner
    {
        public bool ExecuteTasks(Resource resource)
        {
            var tasks = new ResourceTask[]
            {
                new UpdateStreamListTask(),
                new UpdatePackageFileTask(),
                new BuildAssemblyTask()
            };

            var graph = new AdjacencyGraph<string, SEdge<string>>();
            
            // add vertices and edges
            foreach (var task in tasks)
            {
                graph.AddVertex(task.Id);
                graph.AddEdgeRange(task.DependsOn.Select(a => new SEdge<string>(task.Id, a)));
            }

            var runTasks = graph.TopologicalSort().Reverse().Select(a => tasks.First(b => b.Id == a)).Where(a => a.NeedsExecutionFor(resource));

            if (runTasks.FirstOrDefault() != null)
            {
                this.Log().Info("Running tasks on {0}: {1}.", resource.Name, string.Join(", ", runTasks.Select(a => a.Id)));
            }

            foreach (var task in runTasks)
            {
                if (!task.Process(resource))
                {
                    this.Log().Warn("Task {0} failed.", task.Id);
                    return false;
                }
            }

            return true;
        }
    }
}
