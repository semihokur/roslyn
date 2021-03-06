﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports TypeKind = Microsoft.CodeAnalysis.TypeKind

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeGen
    Partial Friend Class StackScheduler

        ''' <summary>
        ''' Rewrites the tree to account for destructive nature of stack local reads.
        ''' 
        ''' Typically, last read stays as-is and local is destroyed by the read.
        ''' Intermediate reads are rewritten as Dups -
        ''' 
        '''       NotLastUse(X_stackLocal) ===> NotLastUse(Dup)
        '''       LastUse(X_stackLocal) ===> LastUse(X_stackLocal)
        ''' 
        ''' </summary>
        Private NotInheritable Class Rewriter
            Inherits BoundTreeRewriter

            Private nodeCounter As Integer = 0
            Private ReadOnly info As Dictionary(Of LocalSymbol, LocalDefUseInfo) = Nothing

            Private Sub New(info As Dictionary(Of LocalSymbol, LocalDefUseInfo))
                Me.info = info
            End Sub

            Public Shared Function Rewrite(src As BoundStatement, info As Dictionary(Of LocalSymbol, LocalDefUseInfo)) As BoundStatement
                Dim scheduler As New Rewriter(info)
                Return DirectCast(scheduler.Visit(src), BoundStatement)
            End Function

            Public Overrides Function Visit(node As BoundNode) As BoundNode
                Dim result As BoundNode = Nothing

                ' rewriting constants may undo constant folding and make thing worse.
                ' so we will not go into constant nodes. 
                ' CodeGen will not do that either.
                Dim asExpression = TryCast(node, BoundExpression)
                If asExpression IsNot Nothing AndAlso asExpression.ConstantValueOpt IsNot Nothing Then
                    result = node
                Else
                    result = MyBase.Visit(node)
                End If

                Me.nodeCounter += 1

                Return result
            End Function

            Private Shared Function IsLastAccess(locInfo As LocalDefUseInfo, counter As Integer) As Boolean
                Return locInfo.localDefs.Any(Function(d) counter = d.Start AndAlso counter = d.End)
            End Function

            Public Overrides Function VisitLocal(node As BoundLocal) As BoundNode
                Dim locInfo As LocalDefUseInfo = Nothing
                If Not info.TryGetValue(node.LocalSymbol, locInfo) Then
                    Return MyBase.VisitLocal(node)
                End If

                ' not the last access, emit Dup.
                If Not IsLastAccess(locInfo, nodeCounter) Then
                    Return New BoundDup(node.Syntax, node.LocalSymbol.IsByRef, node.Type)
                End If

                ' last access - leave the node as is. Emit will do nothing expecting the node on the stack
                Return MyBase.VisitLocal(node)
            End Function

            ' TODO: Why??
            'Public Overrides Function VisitObjectCreationExpression(node As BoundObjectCreationExpression) As BoundNode
            '    Dim arguments As ImmutableArray(Of BoundExpression) = Me.VisitList(node.Arguments)
            '    Debug.Assert(node.InitializerOpt Is Nothing)
            '    Dim type As TypeSymbol = Me.VisitType(node.Type)
            '    Return node.Update(node.ConstructorOpt, arguments, node.InitializerOpt, type)
            'End Function

            Public Overrides Function VisitReferenceAssignment(node As BoundReferenceAssignment) As BoundNode
                Dim locInfo As LocalDefUseInfo = Nothing
                Dim left = DirectCast(node.ByRefLocal, BoundLocal)

                ' store to something that is not special. (operands still could be rewritten) 
                If Not info.TryGetValue(left.LocalSymbol, locInfo) Then
                    Return MyBase.VisitReferenceAssignment(node)
                End If

                ' we do not need to vist lhs, just update the counter to be in sync
                Me.nodeCounter += 1

                ' Visit the expression being assigned 
                Dim right = DirectCast(Me.Visit(node.Target), BoundExpression)

                ' this should not be the last store, why would be created such a variable after all???
                Debug.Assert(locInfo.localDefs.Any(Function(d) nodeCounter = d.Start AndAlso nodeCounter <= d.End))
                Debug.Assert(Not IsLastAccess(locInfo, nodeCounter))

                ' assigned local used later - keep assignment. 
                ' codegen will keep value on stack when sees assignment "stackLocal = expr"
                Return node.Update(left, right, node.IsLValue, node.Type)
            End Function

            ' default visitor for AssignmentOperator that ignores LeftOnTheRightOpt
            Private Function VisitAssignmentOperatorDefault(node As BoundAssignmentOperator) As BoundNode
                Dim left As BoundExpression = DirectCast(Me.Visit(node.Left), BoundExpression)
                Dim right As BoundExpression = DirectCast(Me.Visit(node.Right), BoundExpression)

                Debug.Assert(node.LeftOnTheRightOpt Is Nothing)

                Return node.Update(left, Nothing, right, node.SuppressObjectClone, node.Type)
            End Function

            Public Overrides Function VisitAssignmentOperator(node As BoundAssignmentOperator) As BoundNode
                Dim locInfo As LocalDefUseInfo = Nothing
                Dim left = TryCast(node.Left, BoundLocal)

                ' store to something that is not special. (operands still could be rewritten) 
                If left Is Nothing OrElse Not info.TryGetValue(left.LocalSymbol, locInfo) Then
                    Return VisitAssignmentOperatorDefault(node)
                End If

                ' indirect local store is not special. (operands still could be rewritten) 
                ' NOTE: if Lhs is a stack local, it will be handled as a read and possibly duped.
                If Analyzer.IsIndirectAssignment(node) Then
                    Return VisitAssignmentOperatorDefault(node)
                End If

                '==  here we have a regular write to a stack local
                '
                ' we do not need to vist lhs, because we do not read the local,
                ' just update the counter to be in sync.
                ' 
                ' if this is the last store, we just push the rhs
                ' otherwise record a store.

                ' fake visiting of left
                Me.nodeCounter += 1

                ' Left on the right should be Nothing by this time
                Debug.Assert(node.LeftOnTheRightOpt Is Nothing)

                ' do not visit left-on-the-right
                ' Me.nodeCounter += 1

                ' visit right
                Dim right = DirectCast(Me.Visit(node.Right), BoundExpression)

                ' do actual assignment

                Debug.Assert(locInfo.localDefs.Any(Function(d) nodeCounter = d.Start AndAlso nodeCounter <= d.End))
                Dim isLast As Boolean = IsLastAccess(locInfo, nodeCounter)

                If isLast Then
                    ' assigned local is not used later => just emit the Right 
                    Return right

                Else
                    ' assigned local used later - keep assignment. 
                    ' codegen will keep value on stack when sees assignment "stackLocal = expr"
                    Return node.Update(left, node.LeftOnTheRightOpt, right, node.SuppressObjectClone, node.Type)
                End If
            End Function

        End Class

    End Class
End Namespace

