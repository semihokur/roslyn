﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public abstract class CodeFixTestBase : DiagnosticAnalyzerTestBase
    {
        protected abstract ICodeFixProvider GetCSharpCodeFixProvider();

        protected abstract ICodeFixProvider GetBasicCodeFixProvider();

        protected void VerifyCSharpFix(string oldSource, string newSource, int? codeFixIndex = null, bool allowNewCompilerDiagnostics = false, bool continueOnError = false)
        {
            VerifyFix(LanguageNames.CSharp, GetCSharpDiagnosticAnalyzer(), GetCSharpCodeFixProvider(), oldSource, newSource, codeFixIndex, allowNewCompilerDiagnostics, continueOnError);
        }

        protected void VerifyBasicFix(string oldSource, string newSource, int? codeFixIndex = null, bool allowNewCompilerDiagnostics = false, bool continueOnError = false)
        {
            VerifyFix(LanguageNames.VisualBasic, GetBasicDiagnosticAnalyzer(), GetBasicCodeFixProvider(), oldSource, newSource, codeFixIndex, allowNewCompilerDiagnostics, continueOnError);
        }

        protected void VerifyFix(string language, IDiagnosticAnalyzer analyzer, ICodeFixProvider codeFixProvider, string oldSource, string newSource, int? codeFixIndex, bool allowNewCompilerDiagnostics, bool continueOnError)
        {
            var document = CreateDocument(oldSource, language);

            VerifyFix(document, analyzer, codeFixProvider, newSource, codeFixIndex, useCompilerAnalyzerDriver: true, allowNewCompilerDiagnostics: allowNewCompilerDiagnostics, continueOnError: continueOnError);
            VerifyFix(document, analyzer, codeFixProvider, newSource, codeFixIndex, useCompilerAnalyzerDriver: false, allowNewCompilerDiagnostics: allowNewCompilerDiagnostics, continueOnError: continueOnError);
        }

        private void VerifyFix(Document document, IDiagnosticAnalyzer analyzer, ICodeFixProvider codeFixProvider, string newSource, int? codeFixIndex, bool useCompilerAnalyzerDriver, bool allowNewCompilerDiagnostics, bool continueOnError)
        {
            var analyzerDiagnostics = GetSortedDiagnostics(analyzer, document, useCompilerAnalyzerDriver: useCompilerAnalyzerDriver, continueOnError: continueOnError);
            var compilerDiagnostics = document.GetSemanticModelAsync().Result.GetDiagnostics();

            // TODO(mavasani): Delete the below if statement once FxCop Analyzers have been ported to new IDiagnosticAnalyzer API.
            if (!useCompilerAnalyzerDriver)
            {
                Assert.True(analyzerDiagnostics.IsEmpty());
                return;
            }

            var attempts = analyzerDiagnostics.Length;

            for (int i = 0; i < attempts; ++i)
            {
                var actions = codeFixProvider.GetFixesAsync(document, analyzerDiagnostics[0].Location.SourceSpan, analyzerDiagnostics, CancellationToken.None).Result;
                if (!actions.Any())
                {
                    break;
                }

                if (codeFixIndex != null)
                {
                    document = document.Apply(actions.ElementAt((int)codeFixIndex));
                    break;
                }

                document = document.Apply(actions.ElementAt(0));
                
                analyzerDiagnostics = GetSortedDiagnostics(analyzer, document, useCompilerAnalyzerDriver: useCompilerAnalyzerDriver, continueOnError: continueOnError);
                var newCompilerDiagnostics = GetNewDiagnostics(compilerDiagnostics, document.GetSemanticModelAsync().Result.GetDiagnostics());
                if (!allowNewCompilerDiagnostics && newCompilerDiagnostics.Any())
                {
                    // Format and get the compiler diagnostics again so that the locations make sense in the output
                    document = document.WithSyntaxRoot(Formatter.Format(document.GetSyntaxRootAsync().Result, Formatter.Annotation, document.Project.Solution.Workspace));
                    newCompilerDiagnostics = GetNewDiagnostics(compilerDiagnostics, document.GetSemanticModelAsync().Result.GetDiagnostics());

                    Assert.True(false,
                        string.Format("Fix introduced new compiler diagnostics:\r\n{0}\r\n\r\nNew document:\r\n{1}\r\n",
                            newCompilerDiagnostics.Select(d => d.ToString()).Join("\r\n"),
                            document.GetSyntaxRootAsync().Result.ToFullString()));
                }

                if (analyzerDiagnostics.IsEmpty())
                {
                    break;
                }
            }

            var newDocument = Simplifier.ReduceAsync(document, Simplifier.Annotation).Result;
            var root = newDocument.GetSyntaxRootAsync().Result;
            root = Formatter.Format(root, Formatter.Annotation, newDocument.Project.Solution.Workspace);
            var actual = root.GetText().ToString();
            Assert.Equal(newSource, actual);
        }

        private static IEnumerable<Diagnostic> GetNewDiagnostics(IEnumerable<Diagnostic> diagnostics, IEnumerable<Diagnostic> newDiagnostics)
        {
            var oldArray = diagnostics.OrderBy(d => d.Location.SourceSpan.Start).ToArray();
            var newArray = newDiagnostics.OrderBy(d => d.Location.SourceSpan.Start).ToArray();

            int oldIndex = 0;
            int newIndex = 0;

            while (newIndex < newArray.Length)
            {
                if (oldIndex < oldArray.Length && oldArray[oldIndex].Id == newArray[newIndex].Id)
                {
                    ++oldIndex;
                    ++newIndex;
                }
                else
                {
                    yield return newArray[newIndex++];
                }
            }
        }

        private static Diagnostic[] GetSortedDiagnostics(IDiagnosticAnalyzer analyzerFactory, Document document, bool useCompilerAnalyzerDriver, bool continueOnError, TextSpan? span = null)
        {
            TextSpan spanToTest = span.HasValue ? span.Value : document.GetSyntaxRootAsync().Result.FullSpan;

            var diagnostics = useCompilerAnalyzerDriver ?
                GetDiagnosticsUsingCompilerAnalyzerDriver(analyzerFactory, document, continueOnError, span) :
                GetDiagnosticsUsingIDEAnalyzerDriver(analyzerFactory, document, span);

            return GetSortedDiagnostics(diagnostics);
        }

        private static IEnumerable<Diagnostic> GetDiagnosticsUsingIDEAnalyzerDriver(IDiagnosticAnalyzer analyzer, Document document, TextSpan? span)
        {
            // TODO(mavasani): Uncomment the below code once FxCop Analyzers have been ported to new IDiagnosticAnalyzer API.

            ////return includeProjectDiagnostics ?
            ////    DiagnosticProviderTestUtilities.GetAllDiagnostics(analyzerFactory, document, span) :
            ////    DiagnosticProviderTestUtilities.GetDocumentDiagnostics(analyzerFactory, document, span);

            return SpecializedCollections.EmptyEnumerable<Diagnostic>();
        }

        private static IEnumerable<Diagnostic> GetDiagnosticsUsingCompilerAnalyzerDriver(IDiagnosticAnalyzer analyzer, Document document, bool continueOnError, TextSpan? span)
        {
            var semanticModel = document.GetSemanticModelAsync().Result;
            var compilation = semanticModel.Compilation;

            var diagnostics = new List<Diagnostic>();
            Action<Diagnostic> addDiagnostic = diagnostics.Add;

            var compilationStartedAnalyzer = analyzer as ICompilationStartedAnalyzer;
            ICompilationEndedAnalyzer compilationEndedAnalyzer = null;
            if (compilationStartedAnalyzer != null)
            {
                compilationEndedAnalyzer = compilationStartedAnalyzer.OnCompilationStarted(compilation, addDiagnostic, null, default(CancellationToken));
            }

            AnalyzeDocumentCore(analyzer, document, addDiagnostic, span, continueOnError);

            if (compilationEndedAnalyzer != null)
            {
                AnalyzeDocumentCore(compilationEndedAnalyzer, document, addDiagnostic, span, continueOnError);
                compilationEndedAnalyzer.OnCompilationEnded(compilation, addDiagnostic, null, default(CancellationToken));
            }

            return diagnostics;
        }
    }
}