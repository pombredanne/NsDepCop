using System;
using System.Linq;
using Codartis.NsDepCop.Core.Util;
using Microsoft.VisualStudio.Shell;

namespace Codartis.NsDepCop.VisualStudioIntegration
{
    /// <summary>
    /// Finds project files in the current Visual Studio workspace.
    /// </summary>
    public class WorkspaceProjectFileResolver : IProjectFileResolver
    {
        private readonly MessageHandler _traceMessageHandler;

        public WorkspaceProjectFileResolver(MessageHandler traceMessageHandler)
        {
            _traceMessageHandler = traceMessageHandler;
        }

        public string FindByAssemblyName(string assemblyName)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return FindByAssemblyNameCore(assemblyName);
            });
        }

        private string FindByAssemblyNameCore(string assemblyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var workspace = VisualStudioServiceGateway.GetWorkspace();

            var currentSolution = workspace.CurrentSolution;
            if (currentSolution == null)
                throw new Exception("Cannot acquire CurrentSolution.");

            var projectFilePath = currentSolution.Projects.FirstOrDefault(i => i.AssemblyName == assemblyName)?.FilePath;

            LogTraceMessage($"Project file path for '{assemblyName}' is '{projectFilePath}'.");

            return projectFilePath;
        }

        private void LogTraceMessage(string message) => _traceMessageHandler?.Invoke(message);
    }
}
