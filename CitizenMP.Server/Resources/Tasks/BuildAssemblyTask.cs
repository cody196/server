using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            this.Log().Debug("{0} in {1}({2},{3})", e.Message, e.File, e.LineNumber, e.ColumnNumber);
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

        public override async Task<bool> Process(Resource resource)
        {
            var solution = Path.Combine(resource.Path, resource.Info["clr_solution"]);

            var globalProperties = new Dictionary<string, string>();
            globalProperties["Configuration"] = "Release";
            globalProperties["OutputPath"] = Path.GetFullPath(Path.Combine("cache/resource_bin", resource.Name));

            // prepare a build request            
            var buildRequest = new BuildRequestData(Path.GetFullPath(solution), globalProperties, "4.0", new[] { "Build" }, null);

            // set the build parameters
            var buildParameters = new BuildParameters();
            buildParameters.Loggers = new[] { this };

            BuildManager.DefaultBuildManager.BeginBuild(buildParameters);

            // create a build submission and execute
            var buildSubmission = BuildManager.DefaultBuildManager.PendBuildRequest(buildRequest);
            await buildSubmission.ExecuteAsync();

            // end building
            BuildManager.DefaultBuildManager.EndBuild();

            var success = (buildSubmission.BuildResult.OverallResult == BuildResultCode.Success);

            if (success)
            {
                var buildResult = buildSubmission.BuildResult.ResultsByTarget["Build"];

                foreach (var item in buildResult.Items)
                {
                    var baseName = Path.GetFileName(item.ItemSpec);

                    resource.ExternalFiles[string.Format("bin/{0}", baseName)] = new FileInfo(item.ItemSpec);
                }
            }

            return success;
        }
    }

    // from http://blog.nerdbank.net/2011/07/c-await-for-msbuild.html
    public static class BuildSubmissionAwaitExtensions
    {
        public static Task<BuildResult> ExecuteAsync(this BuildSubmission submission)
        {
            var tcs = new TaskCompletionSource<BuildResult>();
            submission.ExecuteAsync(SetBuildComplete, tcs);
            return tcs.Task;
        }

        private static void SetBuildComplete(BuildSubmission submission)
        {
            var tcs = (TaskCompletionSource<BuildResult>)submission.AsyncContext;
            tcs.SetResult(submission.BuildResult);
        }
    }
}
