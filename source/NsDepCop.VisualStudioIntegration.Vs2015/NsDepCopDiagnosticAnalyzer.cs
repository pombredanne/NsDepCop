#pragma warning disable RS1022

using Codartis.NsDepCop.ParserAdapter.Roslyn1x;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Codartis.NsDepCop.VisualStudioIntegration
{
    /// <summary>
    /// This diagnostic analyzer is invoked by Visual Studio/Roslyn and it reports namespace dependency issues to the VS IDE.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NsDepCopDiagnosticAnalyzer : NsDepCopDiagnosticAnalyzerBase
    {
        public NsDepCopDiagnosticAnalyzer()
            : base(new Roslyn1TypeDependencyEnumerator(LogTraceMessage))
        {
        }

        protected override SyntaxKind[] GetSyntaxKindsToRegister()
        {
            return new []
            {
                SyntaxKind.IdentifierName,
                SyntaxKind.GenericName
            };
        }
    }
}

#pragma warning restore RS1022
