﻿using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.CSharp.UnitTests;
using Roslyn.Test.Utilities;
using System.Linq;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.Semantic.UnitTests.Semantics
{
    /// <summary>
    /// Contains tests for expression-bodied members in the semantic model.
    /// </summary>
    public class ExpressionBodiedMemberTests : SemanticModelTestBase
    {
        [Fact]
        public void ExprBodiedProp01()
        {
            var comp = CreateCompilationWithMscorlib45(@"
class Program
{
    public int F = 1;
    public int P2 => /*<bind>*/this.F/*</bind>*/;
}
");
            comp.VerifyDiagnostics();
            var semanticInfo = GetSemanticInfoForTest<MemberAccessExpressionSyntax>(comp);

            Assert.Equal(SpecialType.System_Int32, semanticInfo.Type.SpecialType);
            Assert.Equal(SpecialType.System_Int32, semanticInfo.ConvertedType.SpecialType);
            Assert.Equal(ConversionKind.Identity, semanticInfo.ImplicitConversion.Kind);

            var semanticSymbol = semanticInfo.Symbol;
            var global = comp.GlobalNamespace;
            var program = global.GetTypeMember("Program");
            var field = program.GetMember<SourceFieldSymbol>("F");

            Assert.Equal(field, semanticSymbol);

            Assert.Equal(CandidateReason.None, semanticInfo.CandidateReason);
            Assert.Equal(0, semanticInfo.CandidateSymbols.Length);

            Assert.Equal(0, semanticInfo.MethodGroup.Length);

            Assert.False(semanticInfo.IsCompileTimeConstant);
        }

        [Fact]
        public void ExprBodiedProp03()
        {
            var semanticInfo = GetSemanticInfoForTest<LiteralExpressionSyntax>(@"
using System;

class C
{
    int P => /*<bind>*/10/*</bind>*/;
}");
            Assert.NotNull(semanticInfo);
            Assert.Null(semanticInfo.Symbol);
            Assert.True(semanticInfo.IsCompileTimeConstant);
            Assert.Equal(10, semanticInfo.ConstantValue);
        }

        [Fact]
        public void ExprBodiedProp04()
        {
            var tree = Parse(@"
using System;

class C
{
    int P => /*<bind>*/P/*</bind>*/;
}");
            var comp = CreateCompilationWithMscorlib45(new[] { tree });

            var info = GetSemanticInfoForTest<IdentifierNameSyntax>(comp);
            Assert.NotNull(info);
            var sym = Assert.IsType<SourcePropertySymbol>(info.Symbol);
            var c = comp.GlobalNamespace.GetMember<NamedTypeSymbol>("C");
            Assert.Equal(c.GetMember<SourcePropertySymbol>("P"), sym);
        }

        [Fact]
        public void ExprBodiedIndexer01()
        {
            var semanticInfo = GetSemanticInfoForTest<IdentifierNameSyntax>(@"
using System;
class Program
{
    public int this[int i] => /*<bind>*/i/*</bind>*/;
}");
            Assert.NotNull(semanticInfo);
            Assert.Equal(SpecialType.System_Int32, semanticInfo.Type.SpecialType);
            Assert.Equal("i", semanticInfo.Symbol.Name);
            Assert.Equal(SymbolKind.Parameter, semanticInfo.Symbol.Kind);
            Assert.Equal(0, semanticInfo.CandidateSymbols.Length);
            Assert.False(semanticInfo.IsCompileTimeConstant);
        }

        [Fact]
        public void ExprBodiedIndexer02()
        {
            var semanticInfo = GetSemanticInfoForTest<IdentifierNameSyntax>(@"
using System;
class C {}
class Program
{
    public C this[C c] => /*<bind>*/c/*</bind>*/;
}");
            Assert.NotNull(semanticInfo);
            Assert.Equal("C", semanticInfo.Type.Name);
            Assert.Equal("c", semanticInfo.Symbol.Name);
            Assert.Equal(SymbolKind.Parameter, semanticInfo.Symbol.Kind);
            Assert.Equal(0, semanticInfo.CandidateSymbols.Length);
            Assert.False(semanticInfo.IsCompileTimeConstant);
        }

        [Fact]
        public void ExprBodiedIndexer03()
        {
            var semanticInfo = GetSemanticInfoForTest<IdentifierNameSyntax>(@"
class C
{
    public object this[string s] => /*<bind>*/s/*</bind>*/;
}");
            Assert.NotNull(semanticInfo);
            Assert.Equal("s", semanticInfo.Symbol.Name);
            Assert.Equal(SymbolKind.Parameter, semanticInfo.Symbol.Kind);
            Assert.Equal(0, semanticInfo.CandidateSymbols.Length);
            Assert.False(semanticInfo.IsCompileTimeConstant);

            Assert.Equal(SpecialType.System_String, semanticInfo.Type.SpecialType);
            Assert.Equal(SpecialType.System_Object, semanticInfo.ConvertedType.SpecialType);
        }

        [Fact]
        public void ExprBodiedIndexer04()
        {
            var semanticInfo = GetSemanticInfoForTest<InvocationExpressionSyntax>(@"
class C
{
    int M(int i) => i;
    long M(long l) => l;
    string this[string s] => /*<bind>*/M(s)/*</bind>*/;
}");
            Assert.NotNull(semanticInfo);
            Assert.Equal(2, semanticInfo.CandidateSymbols.Length);
            Assert.Equal("System.Int32 C.M(System.Int32 i)", semanticInfo.CandidateSymbols[0].ToTestDisplayString());
            Assert.Equal("System.Int64 C.M(System.Int64 l)", semanticInfo.CandidateSymbols[1].ToTestDisplayString());
            Assert.Equal(CandidateReason.OverloadResolutionFailure, semanticInfo.CandidateReason);
        }

        [Fact]
        public void ExprBodiedIndexer05()
        {
            var semanticInfo = GetSemanticInfoForTest<SimpleNameSyntax>(@"
class C
{
    int this[int i] => /*<bind>*/i/*</bind>*/;
}");
            Assert.NotNull(semanticInfo);
            var sym = semanticInfo.Symbol;
            var accessor = Assert.IsType<SourcePropertyAccessorSymbol>(sym.ContainingSymbol);
            var prop = accessor.AssociatedSymbol;
            Assert.IsType<SourcePropertySymbol>(prop);
        }

        [Fact]
        public void ExprBodiedFunc01()
        {
            var comp = CreateCompilationWithMscorlib45(@"
class Program
{
    public int M(int i) => /*<bind>*/i/*</bind>*/;
}");
            comp.VerifyDiagnostics();

            var semanticInfo = GetSemanticInfoForTest<IdentifierNameSyntax>(comp);

            Assert.Equal(SpecialType.System_Int32, semanticInfo.Type.SpecialType);
            Assert.Equal(SpecialType.System_Int32, semanticInfo.ConvertedType.SpecialType);
            Assert.Equal(ConversionKind.Identity, semanticInfo.ImplicitConversion.Kind);

            var semanticSymbol = semanticInfo.Symbol;
            var global = comp.GlobalNamespace;
            var program = global.GetTypeMember("Program");
            var method = program.GetMember<SourceMemberMethodSymbol>("M");
            var i = method.Parameters[0];

            Assert.Equal(i, semanticSymbol);

            Assert.Equal(CandidateReason.None, semanticInfo.CandidateReason);
            Assert.Equal(0, semanticInfo.CandidateSymbols.Length);

            Assert.Equal(0, semanticInfo.MethodGroup.Length);

            Assert.False(semanticInfo.IsCompileTimeConstant);
        }

        [Fact]
        [WorkItem(1009638)]
        public void ExprBodiedFunc02()
        {
            var comp = CreateCompilationWithMscorlib45(@"
class C
{
    public T M<T>(T t) where T : class => /*<bind>*/t/*</bind>*/;
}");
            comp.VerifyDiagnostics();

            var semanticInfo = GetSemanticInfoForTest<IdentifierNameSyntax>(comp);

            Assert.Equal(TypeKind.TypeParameter, semanticInfo.Type.TypeKind);
            Assert.Equal("T", semanticInfo.Type.Name);
            Assert.Equal("t", semanticInfo.Symbol.Name);
            var m = semanticInfo.Symbol.ContainingSymbol as SourceMemberMethodSymbol;
            Assert.Equal(1, m.TypeParameters.Length);
            Assert.Equal(m.TypeParameters[0], semanticInfo.Type);
            Assert.Equal(m.TypeParameters[0], m.ReturnType);
            Assert.Equal(m, semanticInfo.Type.ContainingSymbol);
            Assert.Equal(SymbolKind.Parameter, semanticInfo.Symbol.Kind);
        }

        [Fact]
        public void ExprBodiedOperator01()
        {
            var comp = CreateCompilationWithMscorlib45(@"
class Program
{
    public static Program operator ++(Program p) => /*<bind>*/p/*</bind>*/;
}");
            comp.VerifyDiagnostics();

            var semanticInfo = GetSemanticInfoForTest<IdentifierNameSyntax>(comp);

            Assert.Equal("Program", semanticInfo.Type.Name);
            Assert.Equal("Program", semanticInfo.ConvertedType.Name);
            Assert.Equal(ConversionKind.Identity, semanticInfo.ImplicitConversion.Kind);

            var semanticSymbol = semanticInfo.Symbol;
            var global = comp.GlobalNamespace;
            var program = global.GetTypeMember("Program");
            var method = program.GetMember<SourceUserDefinedOperatorSymbol>("op_Increment");
            var p = method.Parameters[0];

            Assert.Equal(p, semanticSymbol);

            Assert.Equal(CandidateReason.None, semanticInfo.CandidateReason);
            Assert.Equal(0, semanticInfo.CandidateSymbols.Length);

            Assert.Equal(0, semanticInfo.MethodGroup.Length);

            Assert.False(semanticInfo.IsCompileTimeConstant);
        }

        [Fact]
        public void ExprBodiedConversion01()
        {
            var comp = CreateCompilationWithMscorlib45(@"
class C
{
    public static C M(int i) => new C();
    public static explicit operator C(int i) => M(/*<bind>*/i/*</bind>*/);
}");
            comp.VerifyDiagnostics();

            var semanticInfo = GetSemanticInfoForTest<IdentifierNameSyntax>(comp);

            Assert.Equal(SpecialType.System_Int32, semanticInfo.Type.SpecialType);
            Assert.Equal(SpecialType.System_Int32, semanticInfo.ConvertedType.SpecialType);
            Assert.Equal(ConversionKind.Identity, semanticInfo.ImplicitConversion.Kind);

            var semanticSymbol = semanticInfo.Symbol;
            var global = comp.GlobalNamespace;
            var program = global.GetTypeMember("C");
            var method = program.GetMember<SourceUserDefinedConversionSymbol>("op_Explicit");
            var p = method.Parameters[0];

            Assert.Equal(p, semanticSymbol);

            Assert.Equal(CandidateReason.None, semanticInfo.CandidateReason);
            Assert.Equal(0, semanticInfo.CandidateSymbols.Length);

            Assert.Equal(0, semanticInfo.MethodGroup.Length);

            Assert.False(semanticInfo.IsCompileTimeConstant);
        }

        [Fact, WorkItem(1069421, "DevDiv")]
        public void Bug1069421()
        {
            var comp = CreateCompilationWithMscorlib45(@"
class Program
{
    private int x => () => { 
}
");

            var tree = comp.SyntaxTrees[0];
            var model = comp.GetSemanticModel(tree);

            var node = tree.GetRoot().DescendantNodes().OfType<ParenthesizedLambdaExpressionSyntax>().Single();

            var typeInfo1 = model.GetTypeInfo(node);
            var typeInfo2 = model.GetTypeInfo(node);

            Assert.Equal(typeInfo1.Type, typeInfo2.Type);
        }

    }
}
