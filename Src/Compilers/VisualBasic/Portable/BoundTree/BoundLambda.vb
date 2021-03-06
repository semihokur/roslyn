﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Class BoundLambda

        ''' <summary>
        ''' Should this lambda be treated as a single line lambda?
        ''' </summary>
        Public ReadOnly Property IsSingleLine As Boolean
            Get
                ' NOTE: the following assert fails if the lambda was compiler generated, this is 
                ' NOTE: intentional as the method is not supposed to be called for such lambdas
                Debug.Assert(TypeOf Me.Syntax Is LambdaExpressionSyntax)

                Dim kind As SyntaxKind = Me.Syntax.Kind

                Return kind = SyntaxKind.SingleLineFunctionLambdaExpression OrElse
                       kind = SyntaxKind.SingleLineSubLambdaExpression
            End Get
        End Property

        Public Overrides ReadOnly Property ExpressionSymbol As Symbol
            Get
                Return Me.LambdaSymbol
            End Get
        End Property

#If DEBUG Then
        Private Sub Validate()
            Debug.Assert((DelegateRelaxation And (Not ConversionKind.DelegateRelaxationLevelMask)) = 0)
        End Sub
#End If
    End Class

End Namespace
