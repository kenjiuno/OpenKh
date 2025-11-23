using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record DeclarationStatementForAssignmentVisitor(
        Func<Antlr4.Runtime.ParserRuleContext, SourceRef> GetSourceRef,
        CHelper CHelper,
        Func<CPP14Parser.DeclSpecifierSeqContext, TypeDescAndStorage> ReduceTypeDesc2,
        ExpressionToVarRefVisitor ExpressionToVarRefVisitor)
    {
        internal void Visit(CPP14Parser.DeclarationStatementContext declarationStatement, WriteHelper writeHelper)
        {
            if (declarationStatement.blockDeclaration() is var blockDeclaration
                && blockDeclaration != null)
            {
                VisitBlockDeclaration(blockDeclaration, writeHelper);
                return;
            }

            throw new CompilerException(GetSourceRef(declarationStatement), "Unsupported declaration statement");
        }

        internal void VisitBlockDeclaration(CPP14Parser.BlockDeclarationContext blockDeclaration, WriteHelper writeHelper)
        {
            if (blockDeclaration.simpleDeclaration() is var simpleDeclaration
                && simpleDeclaration != null)
            {
                VisitSimpleDeclaration(simpleDeclaration, writeHelper);
                return;
            }

            throw new CompilerException(GetSourceRef(blockDeclaration), "Unsupported block declaration");
        }

        private void VisitSimpleDeclaration(CPP14Parser.SimpleDeclarationContext simpleDeclaration, WriteHelper writeHelper)
        {
            var handled = false;

            TypeDescAndStorage? varTypePair = null;

            if (simpleDeclaration.declSpecifierSeq() is var declSpecifierSeq
                && declSpecifierSeq != null)
            {
                varTypePair = ReduceTypeDesc2(declSpecifierSeq);
            }

            if (simpleDeclaration.initDeclaratorList() is var initDeclaratorList
                && initDeclaratorList != null)
            {
                VisitInitDeclaratorList(initDeclaratorList, writeHelper, varTypePair);
                return;
            }

            throw new CompilerException(GetSourceRef(simpleDeclaration), "Unsupported simple declaration");
        }

        private void VisitInitDeclaratorList(
            CPP14Parser.InitDeclaratorListContext initDeclaratorList,
            WriteHelper writeHelper,
            TypeDescAndStorage? varTypePair = null)
        {
            if (initDeclaratorList.initDeclarator() is var initDeclarators
                && initDeclarators != null)
            {
                var parameterDeclarationToNameAndTypePairVisitor = new ParameterDeclarationToNameAndTypePairVisitor(
                    ReduceTypeDesc2,
                    GetSourceRef
                );

                foreach (var initDeclarator in initDeclarators)
                {
                    VarRef? assignTo = null;

                    if (initDeclarator.declarator() is var declarator
                        && declarator != null)
                    {
                        if (varTypePair != null)
                        {
                            // int a;
                            // int a = 1;

                            var varPair = parameterDeclarationToNameAndTypePairVisitor.ReduceDeclarator(
                                declarator,
                                varTypePair.TypeDesc
                            );

                            if (varTypePair.Static)
                            {
                                assignTo = CHelper.DeclareStaticVar(varPair, GetSourceRef(declarator));
                            }
                            else
                            {
                                assignTo = CHelper.DeclareVar(varPair, GetSourceRef(declarator));
                            }
                        }
                        else
                        {
                            // a = 1;

                            var varPair = parameterDeclarationToNameAndTypePairVisitor.ReduceDeclarator(
                                declarator,
                                TypeDesc.Void
                            );

                            assignTo = writeHelper.CHelper.GetVar(varPair.Name);
                        }
                    }

                    if (initDeclarator.initializer() is var initializer
                        && initializer != null)
                    {
                        var rval = VisitInitializer(initializer);
                        if (rval != null)
                        {
                            if (rval.Getter == null)
                            {
                                throw new CompilerException(GetSourceRef(initializer), "Rvalue has no getter");
                            }

                            if (assignTo == null)
                            {
                                throw new CompilerException(GetSourceRef(initDeclarator), "No lvalue to assign to");
                            }

                            if (assignTo.Setter == null)
                            {
                                throw new CompilerException(GetSourceRef(initDeclarator), "Lvalue has no setter");
                            }

                            var converter = CHelper.GetConverter(FromType: rval.Type, ToType: assignTo.Type)
                                ?? throw new CompilerException(
                                    GetSourceRef(initDeclarator),
                                    $"No converter from {rval.Type.Display} to {assignTo.Type.Display}"
                                );

                            var writeTo = (varTypePair?.Static ?? false)
                                ? writeHelper.InitCodeSeg
                                : writeHelper.CodeSeg;

                            writeTo.WriteLine(
                                CHelper.JoinLines(
                                    rval.Getter,
                                    converter,
                                    assignTo.Setter
                                )
                            );
                        }
                    }
                }

                return;
            }

            throw new CompilerException(GetSourceRef(initDeclaratorList), "Unsupported init declarator list");
        }

        private VarRef? VisitInitDeclarator(CPP14Parser.InitDeclaratorContext initDeclarator)
        {
            if (initDeclarator.initializer() is var initializer
                && initializer != null)
            {
                return VisitInitializer(initializer);
            }

            throw new CompilerException(GetSourceRef(initDeclarator), "Unsupported init declarator");
        }

        private VarRef? VisitInitializer(CPP14Parser.InitializerContext initializer)
        {
            if (initializer.braceOrEqualInitializer() is var braceOrEqualInitializer
                && braceOrEqualInitializer != null)
            {
                return VisitBraceOrEqualInitializer(braceOrEqualInitializer);
            }

            throw new CompilerException(GetSourceRef(initializer), "Unsupported initializer");
        }

        private VarRef? VisitBraceOrEqualInitializer(CPP14Parser.BraceOrEqualInitializerContext braceOrEqualInitializer)
        {
            if (braceOrEqualInitializer.initializerClause() is var initializerClause
                && initializerClause != null)
            {
                return VisitInitializerClause(initializerClause);
            }

            throw new CompilerException(GetSourceRef(braceOrEqualInitializer), "Unsupported brace or equal initializer");
        }

        private VarRef? VisitInitializerClause(CPP14Parser.InitializerClauseContext initializerClause)
        {
            if (initializerClause.assignmentExpression() is var assignmentExpression
                && assignmentExpression != null)
            {
                var expr = ExpressionToVarRefVisitor
                    .VisitAssignment(assignmentExpression);

                if (expr.Getter == null)
                {
                    throw new CompilerException(GetSourceRef(assignmentExpression), "Need to produce getter");
                }

                return expr;
            }

            throw new CompilerException(GetSourceRef(initializerClause), "Unsupported initializer clause");
        }
    }
}
