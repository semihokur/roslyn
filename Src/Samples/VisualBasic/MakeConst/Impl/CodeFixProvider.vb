' *********************************************************
'
' Copyright � Microsoft Corporation
'
' Licensed under the Apache License, Version 2.0 (the
' "License"); you may not use this file except in
' compliance with the License. You may obtain a copy of
' the License at
'
' http://www.apache.org/licenses/LICENSE-2.0 
'
' THIS CODE IS PROVIDED ON AN *AS IS* BASIS, WITHOUT WARRANTIES
' OR CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED,
' INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES
' OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR
' PURPOSE, MERCHANTABILITY OR NON-INFRINGEMENT.
'
' See the Apache 2 License for the specific language
' governing permissions and limitations under the License.
'
' *********************************************************

Imports System.Collections.Immutable
Imports System.Composition
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeActions
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

<ExportCodeFixProvider("MakeConstVB", LanguageNames.VisualBasic), [Shared]>
Class MakeConstCodeFixProvider
    Inherits CodeFixProvider

    Public NotOverridable Overrides Function GetFixableDiagnosticIds() As ImmutableArray(Of String)
        Return ImmutableArray.Create(DiagnosticAnalyzer.MakeConstDiagnosticId)
    End Function

    Public NotOverridable Overrides Function GetFixAllProvider() As FixAllProvider
        Return Nothing
    End Function

    Public NotOverridable Overrides Async Function ComputeFixesAsync(context As CodeFixContext) As Task
        Dim diagnostic = context.Diagnostics.First()
        Dim diagnosticSpan = diagnostic.Location.SourceSpan
        Dim root = Await context.Document.GetSyntaxRootAsync(context.CancellationToken)

        ' Find the local declaration identified by the diagnostic.
        Dim declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType(Of LocalDeclarationStatementSyntax)().First()

        ' Register a code action that will invoke the fix.
        context.RegisterFix(CodeAction.Create("Make constant", Function(c) MakeConstAsync(context.Document, declaration, c)), diagnostic)
    End Function

    Private Async Function MakeConstAsync(document As Document, localDeclaration As LocalDeclarationStatementSyntax, cancellationToken As CancellationToken) As Task(Of Document)
        ' Create a const token with the leading trivia from the local declaration.
        Dim firstToken = localDeclaration.GetFirstToken()
        Dim constToken = SyntaxFactory.Token(
            firstToken.LeadingTrivia, SyntaxKind.ConstKeyword, firstToken.TrailingTrivia)

        ' Create a new modifier list with the const token.
        Dim newModifiers = SyntaxFactory.TokenList(constToken)

        ' Produce new local declaration.
        Dim newLocalDeclaration = localDeclaration.WithModifiers(newModifiers)

        ' Add an annotation to format the new local declaration.
        Dim formattedLocalDeclaration = newLocalDeclaration.WithAdditionalAnnotations(Formatter.Annotation)

        ' Replace the old local declaration with the new local declaration.
        Dim root = Await document.GetSyntaxRootAsync(cancellationToken)
        Dim newRoot = root.ReplaceNode(localDeclaration, formattedLocalDeclaration)

        ' Return document with transformed tree.
        Return document.WithSyntaxRoot(newRoot)
    End Function
End Class