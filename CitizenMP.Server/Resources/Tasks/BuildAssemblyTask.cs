using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace CitizenMP.Server.Resources.Tasks
{
    class BuildAssemblyTask : ResourceTask, ILogger
    {
        public override IEnumerable<string> DependsOn
        {
            get { return new string[0]; }
        }

        public override bool NeedsExecutionFor(Resource resource)
        {
            if (resource.Info.ContainsKey("clr_solution"))
            {
                var solution = Path.Combine(resource.Path, resource.Info["clr_solution"]);

                if (File.Exists(solution))
                {
                    return true;
                }
                else
                {
                    this.Log().Warn("Solution {1} for resource {0} does not exist.", resource.Name, solution);
                }
            }

            return false;
        }

        #region ILogger methods
        public string Parameters { get; set; }
        public LoggerVerbosity Verbosity { get; set; }

        private bool WasError { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            eventSource.ProjectStarted += ProjectStarted;
            eventSource.TargetStarted += TargetStarted;
            eventSource.TaskStarted += TaskStarted;
            eventSource.ProjectFinished += ProjectFinished;
            eventSource.TargetFinished += TargetFinished;
            eventSource.TaskFinished += TaskFinished;
            eventSource.MessageRaised += MessageRaised;
            eventSource.WarningRaised += WarningRaised;
            eventSource.StatusEventRaised += StatusEventRaised;
            eventSource.ErrorRaised += ErrorRaised;
        }

        void ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            this.Log().Error("{0} in {1}({2},{3})", e.Message, e.File, e.LineNumber, e.ColumnNumber);

            WasError = true;
        }

        void TaskFinished(object sender, TaskFinishedEventArgs e)
        {
            this.Log().Info("Finished task {0}.", e.TaskName);
        }

        void ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            this.Log().Info("Finished build for project {0}.", e.ProjectFile);
        }

        void TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            this.Log().Info("Finished build for target {0}.", e.TargetName);
        }

        void StatusEventRaised(object sender, BuildStatusEventArgs e)
        {
            this.Log().Debug("{0}", e.Message);
        }

        void MessageRaised(object sender, BuildMessageEventArgs e)
        {
            if (!WasError)
            {
                this.Log().Debug("{0}", e.Message);
            }
            else
            {
                this.Log().Error("{0}", e.Message);

                WasError = false;
            }
        }

        void WarningRaised(object sender, BuildWarningEventArgs e)
        {
            this.Log().Warn("{0} in {1}({2},{3})", e.Message, e.File, e.LineNumber, e.ColumnNumber);
        }

        void TaskStarted(object sender, TaskStartedEventArgs e)
        {
            this.Log().Info("Started task {0} - {1}.", e.TaskName, e.Message);
        }

        void TargetStarted(object sender, TargetStartedEventArgs e)
        {
            this.Log().Info("Started build for target {0}.", e.TargetName);
        }

        void ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            this.Log().Info("Started build for project {0}.", e.ProjectFile);
        }

        public void Shutdown()
        {

        }
        #endregion

        private static object _compileLock = new object();

        public override async Task<bool> Process(Resource resource)
        {
            var solution = Path.Combine(resource.Path, resource.Info["clr_solution"]);

            // set global properties
            var globalProperties = new Dictionary<string, string>();
            globalProperties["Configuration"] = "Release";
            globalProperties["OutputPath"] = Path.GetFullPath(Path.Combine("cache/resource_bin", resource.Name)) + "/";
            globalProperties["IntermediateOutputPath"] = Path.GetFullPath(Path.Combine("cache/resource_obj", resource.Name)) + "/";
            globalProperties["OutDir"] = globalProperties["OutputPath"]; // for Mono?

            try
            {
                // prepare the project file
                var projectRoot = ProjectRootElement.Open(Path.GetFullPath(solution));

                // disable any Citizen profiles
                foreach (var property in projectRoot.Properties)
                {
                    if (property.Name == "TargetFrameworkProfile")
                    {
                        property.Value = "";
                    }
                }

                // we don't want the system mscorlib
                projectRoot.AddProperty("NoStdLib", "true");

                // add hint paths for all references
                Func<string, string> getPath = a => Path.GetFullPath(Path.Combine("system/clrcore", a + ".dll"));

                foreach (var item in projectRoot.Items)
                {
                    if (item.ItemType == "Reference")
                    {
                        // remove existing hint paths
                        item.Metadata.Where(a => a.Name == "HintPath").ToList().ForEach(a => item.RemoveChild(a));

                        item.AddMetadata("HintPath", getPath(item.Include));
                        item.AddMetadata("Private", "false");
                    }
                }

                // add our own mscorlib
                projectRoot.AddItem("Reference", "mscorlib", new Dictionary<string, string>() {
                    { "HintPath", getPath("mscorlib") },
                    { "Private", "false" }
                });

                // create an instance and build request
                var projectInstance = new ProjectInstance(projectRoot, globalProperties, "4.0", ProjectCollection.GlobalProjectCollection);
                var buildRequest = new BuildRequestData(projectInstance, new[] { "Build" }, null);

                BuildSubmission buildSubmission = null;

                // run this in a separate task
                await Task.Run(() =>
                {
                    // building is mutually-exclusive thanks to MSBuild working directory affinity
                    lock (_compileLock)
                    {
                        // set the working directory
                        var oldDirectory = Environment.CurrentDirectory;
                        Environment.CurrentDirectory = Path.GetFullPath(Path.GetDirectoryName(solution));

                        // set the build parameters
                        var buildParameters = new BuildParameters();
                        buildParameters.Loggers = new[] { this };

                        BuildManager.DefaultBuildManager.BeginBuild(buildParameters);

                        // create a build submission and execute
                        buildSubmission = BuildManager.DefaultBuildManager.PendBuildRequest(buildRequest);
                        buildSubmission.Execute();

                        // end building
                        BuildManager.DefaultBuildManager.EndBuild();

                        // put the current directory back
                        Environment.CurrentDirectory = oldDirectory;
                    }
                });

                this.Log().Info("Build for {0} complete - result: {1}", resource.Name, buildSubmission.BuildResult.OverallResult);

                var success = (buildSubmission.BuildResult.OverallResult == BuildResultCode.Success);

                if (success)
                {
                    // the targets producing 'interesting' items (resource_bin/*.dll)
                    var targetList = new[] { "Build", "CoreBuild" }; // CoreBuild is needed on Mono

                    // callback to run on each target
                    Action<TargetResult> iterateResult = buildResult =>
                    {
                        if (buildResult.Items != null)
                        {
                            foreach (var item in buildResult.Items)
                            {
                                var baseName = Path.GetFileName(item.ItemSpec);

                                resource.ExternalFiles[string.Format("bin/{0}", baseName)] = new FileInfo(item.ItemSpec);
                            }
                        }
                    };

                    // loop through the targets
                    foreach (var target in targetList)
                    {
                        TargetResult result;

                        // if it's a resulting target
                        if (buildSubmission.BuildResult.ResultsByTarget.TryGetValue(target, out result))
                        {
                            iterateResult(result);
                        }
                    }
                }

                // unload the project so it will not get cached
                ProjectCollection.GlobalProjectCollection.UnloadProject(projectRoot);

                return success;
            }
            catch (Exception e)
            {
                this.Log().Error(() => "Building assembly failed: " + e.Message, e);

                return false;
            }
        }
    }
}
