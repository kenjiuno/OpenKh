using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record TranslationUnitToCVisitor(
        Func<ParserRuleContext, SourceRef> GetSourceRef,
        ReduceTypeDesc ReduceTypeDesc1)
    {
        internal record Result(
            TextWriter CodeSeg,
            TextWriter InitCodeSeg,
            TextWriter StaticDataSeg,
            TextWriter BssSeg
            );

        internal Result Visit(CPP14Parser.TranslationUnitContext translationUnit)
        {
            if (translationUnit.declarationseq() is var declarationseq
                && declarationseq != null)
            {
                return VisitDeclarationseq(declarationseq);
            }

            throw new CompilerException(GetSourceRef(translationUnit), "Unsupported translation unit");
        }

        private Result VisitDeclarationseq(CPP14Parser.DeclarationseqContext declarationseq)
        {
            var codeSeg = new StringWriter();
            var initCodeSeg = new StringWriter();
            var staticDataSeg = new StringWriter();
            var bssSeg = new StringWriter();

            var globalVarRefDict = new Dictionary<string, VarRef>();

            var cHelper = new CHelper(
                GetVar: varName => globalVarRefDict.TryGetValue(varName, out VarRef? expr)
                    ? expr
                    : null,
                DeclareVar: (argPair, sourceRef) =>
                {
                    var size = argPair.Type.Size;
                    var var1 = globalVarRefDict[argPair.Name] = new VarRef(
                        Type: argPair.Type,
                        Getter: (size <= 4) ? $"push.d.wp {argPair.Name}" : null,
                        Setter: (size <= 4) ? $"pop.wp {argPair.Name}" : null,
                        GetPointer: $"push.wp {argPair.Name}"
                    );
                    var numBytes = (size == 0)
                        ? 4
                        : (size + 3) & (~3);
                    bssSeg.WriteLine($"{argPair.Name}:");
                    bssSeg.WriteLine($" resb {4 * numBytes}");
                    return var1;
                },
                DeclareStaticVar: (argPair, sourceRef) =>
                {
                    var size = argPair.Type.Size;
                    var var1 = globalVarRefDict[argPair.Name] = new VarRef(
                        Type: argPair.Type,
                        Getter: (size <= 4) ? $"push.d.bd {argPair.Name}" : null,
                        Setter: (size <= 4) ? $"pop.bd {argPair.Name}" : null,
                        GetPointer: $"push.bd {argPair.Name}"
                    );
                    var numWords = (size == 0)
                        ? 2
                        : (size + 3) & (~3);
                    staticDataSeg.WriteLine($"{argPair.Name}:");
                    staticDataSeg.WriteLine($" resw {numWords}");
                    return var1;
                },
                AllocateStackItems: _ => throw new NotSupportedException("AllocateStack not use at global scope")
            );

            foreach (var declaration in declarationseq.declaration())
            {
                if (false)
                { }
                else if (declaration.blockDeclaration() is var blockDeclaration && blockDeclaration != null)
                {
                    new DeclarationStatementForAssignmentVisitor(
                        GetSourceRef,
                        cHelper,
                        ReduceTypeDesc1.Reduce,
                        new ExpressionToVarRefVisitor(
                            GetSourceRef,
                            cHelper,
                            ReduceTypeDesc1
                        )
                    )
                        .VisitBlockDeclaration(
                            blockDeclaration,
                            new WriteHelper(
                                CodeSeg: initCodeSeg,
                                InitCodeSeg: initCodeSeg,
                                ExpectedOuts: 0,
                                NextUniqueLabelGenerator: () => throw new NotSupportedException("NextUniqueLabelGenerator not use at global scope"),
                                CHelper: cHelper
                            )
                        );
                }
                else if (declaration.functionDefinition() is var functionDefinition && functionDefinition != null)
                {
                    VisitFunctionDefinition(functionDefinition, codeSeg, initCodeSeg, cHelper);
                }
                else
                {
                    throw new CompilerException(GetSourceRef(declaration), "Unsupported declaration type");
                }
            }

            return new Result(
                CodeSeg: codeSeg,
                InitCodeSeg: initCodeSeg,
                StaticDataSeg: staticDataSeg,
                BssSeg: bssSeg
            );
        }

        private void VisitFunctionDefinition(
            CPP14Parser.FunctionDefinitionContext functionDefinition,
            TextWriter codeSeg,
            TextWriter initCodeSeg,
            CHelper cHelper)
        {
            // type
            TypeDesc retType = ReduceTypeDesc1.Reduce(functionDefinition.declSpecifierSeq()
                ?? throw new CompilerException(GetSourceRef(functionDefinition), "declSpecifierSeq must exist")
            )
                .TypeDesc;

            // name and parameters
            var pointerDeclarator = functionDefinition.declarator().pointerDeclarator();
            var noPointerDeclarator0 = pointerDeclarator.noPointerDeclarator();

            var parameterDeclarations = noPointerDeclarator0?
                .parametersAndQualifiers()?
                .parameterDeclarationClause()?
                .parameterDeclarationList()?
                .parameterDeclaration() ?? Array.Empty<CPP14Parser.ParameterDeclarationContext>();

            var parameterDeclarationToNameAndTypePairVisitor = new ParameterDeclarationToNameAndTypePairVisitor(
                ReduceTypeDesc1.Reduce,
                GetSourceRef
            );

            var retPair = parameterDeclarationToNameAndTypePairVisitor.ReduceDeclarator(
                functionDefinition.declarator(),
                retType
            );

            retType = retPair.Type;

            var funcName = retPair.Name;

            ArgPair[] argPairs = parameterDeclarations
                .Select(parameterDeclarationToNameAndTypePairVisitor.Reduce)
                .ToArray();

            var numLocalStackItemsAllocated = 0; // 4 * n bytes

            var argDict = new Dictionary<string, VarRef>();

            foreach (var argPair in argPairs)
            {
                var spIndex = numLocalStackItemsAllocated;
                var size = argPair.Type.Size;

                argDict[argPair.Name] = new VarRef(
                    Type: argPair.Type,
                    Getter: (size <= 4) ? $"push.d.sp {4 * spIndex} ; {argPair.Name}" : null,
                    Setter: (size <= 4) ? $"pop.sp {4 * spIndex} ; {argPair.Name}" : null,
                    GetPointer: $"push.sp {4 * spIndex} ; &{argPair.Name}"
                );

                numLocalStackItemsAllocated += (size == 0)
                    ? 4
                    : (size + 3) / 4;
            }

            var numArgItemsUsed = numLocalStackItemsAllocated;

            if (256 <= numArgItemsUsed)
            {
                throw new CompilerException(GetSourceRef(functionDefinition), funcName + " has too many arguments");
            }

            if (functionDefinition.functionBody() is var functionBody
                && functionBody != null)
            {
                codeSeg.WriteLine($"{funcName}: ; func {funcName}");

                for (int x = 0; x < numArgItemsUsed; x++)
                {
                    codeSeg.WriteLine($"pop.sp {4 * x}");
                }

                cHelper = cHelper with
                {
                    AllocateStackItems = numItems =>
                    {
                        var spIndex = numLocalStackItemsAllocated;
                        numLocalStackItemsAllocated += numItems;
                        return spIndex;
                    }
                };

                var childScope = NewChildScope(cHelper, argDict);

                int labelIndex = 0;
                WriteStatementsFunctionBody(
                    functionBody,
                    new WriteHelper(
                        CodeSeg: codeSeg,
                        InitCodeSeg: initCodeSeg,
                        ExpectedOuts: (retType != TypeDesc.Void) ? 1 : 0,
                        NextUniqueLabelGenerator: () =>
                        {
                            labelIndex += 1;
                            return purpose =>
                            {
                                var label = $"__{funcName}{labelIndex}{purpose}";
                                return label;
                            };
                        },
                        CHelper: childScope
                    )
                );
            }

            codeSeg.WriteLine("ret");
        }

        private CHelper NewChildScope(
            CHelper parent,
            IDictionary<string, VarRef>? parentDict = null)
        {
            var varRefDict = new Dictionary<string, VarRef?>();

            return new CHelper(
                GetVar: varName =>
                {
                    VarRef? expr = null;
                    if (expr == null)
                    {
                        varRefDict.TryGetValue(varName, out expr);
                    }
                    if (expr == null)
                    {
                        parentDict?.TryGetValue(varName, out expr);
                    }
                    if (expr == null)
                    {
                        expr = parent.GetVar(varName);
                    }
                    return expr;
                },
                DeclareVar: (argPair, sourceRef) =>
                {
                    var numBytes = argPair.Type.Size;
                    var spIndex = parent.AllocateStackItems(
                        (numBytes == 0)
                            ? 1
                            : (numBytes + 3) / 4
                    );
                    return varRefDict[argPair.Name] = new VarRef(
                        Type: argPair.Type,
                        Getter: (numBytes <= 4) ? $"push.d.sp {4 * spIndex} ; {argPair.Name}" : null,
                        Setter: (numBytes <= 4) ? $"pop.sp {4 * spIndex} ; {argPair.Name}" : null,
                        GetPointer: $"push.sp {4 * spIndex} ; &{argPair.Name}"
                    );
                },
                DeclareStaticVar: parent.DeclareStaticVar,
                AllocateStackItems: parent.AllocateStackItems
            );
        }

        private void WriteStatementsFunctionBody(CPP14Parser.FunctionBodyContext functionBody, WriteHelper writeHelper)
        {
            if (functionBody.compoundStatement() is var compoundStatement
                && compoundStatement != null)
            {
                WriteStatementsCompoundStatement(compoundStatement, writeHelper);
                return;
            }

            throw new CompilerException(GetSourceRef(functionBody), "Unsupported function body");
        }

        private void WriteStatementsCompoundStatement(CPP14Parser.CompoundStatementContext compoundStatement, WriteHelper writeHelper)
        {
            if (compoundStatement.statementSeq() is var statementSeq
                && statementSeq != null)
            {
                WriteStatementsStatementSeq(statementSeq, writeHelper);
                return;
            }

            // empty compound statement will produce no code
        }

        private void WriteStatementsStatementSeq(CPP14Parser.StatementSeqContext statementSeq, WriteHelper writeHelper)
        {
            if (statementSeq.statement() is var statements
                && statements != null)
            {
                writeHelper = writeHelper with
                {
                    CHelper = NewChildScope(writeHelper.CHelper),
                };

                foreach (var statement in statements)
                {
                    WriteStatement(statement, writeHelper);
                }
                return;
            }

            throw new CompilerException(GetSourceRef(statementSeq), "Unsupported statement sequence");
        }

        private void WriteStatement(CPP14Parser.StatementContext statement, WriteHelper writeHelper)
        {
            var codeSeg = writeHelper.CodeSeg;

            var cHelper = writeHelper.CHelper;

            if (statement.selectionStatement() is var selectionStatement
                && selectionStatement != null)
            {
                if (false)
                { }
                else if (selectionStatement.If() != null)
                {
                    var expr = new ExpressionToVarRefVisitor(GetSourceRef, cHelper, ReduceTypeDesc1)
                        .Visit(selectionStatement.condition());

                    if (expr.Getter == null)
                    {
                        throw new CompilerException(GetSourceRef(selectionStatement), "Need to produce getter");
                    }

                    var innerStatements = selectionStatement.statement();

                    var labelGen = writeHelper.NextUniqueLabelGenerator();
                    var elseLabel = labelGen("else");
                    var endifLabel = labelGen("endif");
                    codeSeg.WriteLine($"{expr.Getter}");
                    codeSeg.WriteLine($"jz {elseLabel}");
                    if (1 <= innerStatements?.Length
                        && innerStatements[0] is var thenStatement
                        && thenStatement != null)
                    {
                        WriteStatement(thenStatement, writeHelper);
                    }
                    codeSeg.WriteLine($" b {endifLabel}");
                    codeSeg.WriteLine($"{elseLabel}:");
                    if (2 <= innerStatements?.Length
                        && innerStatements[1] is var elseStatement
                        && elseStatement != null)
                    {
                        WriteStatement(elseStatement, writeHelper);
                    }
                    codeSeg.WriteLine($"{endifLabel}:");

                    return;
                }
            }

            if (statement.jumpStatement() is var jumpStatement
                && jumpStatement != null)
            {
                WriteStatementJumpStatement(jumpStatement, writeHelper);
                return;
            }

            if (statement.compoundStatement() is var compoundStatement
                && compoundStatement != null)
            {
                WriteStatementsCompoundStatement(compoundStatement, writeHelper);
                return;
            }

            if (statement.expressionStatement() is var expressionStatement
                && expressionStatement != null)
            {
                if (expressionStatement.expression() is var expression
                    && expression != null)
                {
                    var expr = new ExpressionToVarRefVisitor(GetSourceRef, cHelper, ReduceTypeDesc1)
                        .VisitExpression(expression);

                    if (expr.Getter != null)
                    {
                        codeSeg.WriteLine(expr.Getter);
                        codeSeg.WriteLine("drop");
                    }
                }
                return;
            }

            if (statement.declarationStatement() is var declarationStatement
                && declarationStatement != null)
            {
                new DeclarationStatementForAssignmentVisitor(
                    GetSourceRef,
                    cHelper,
                    ReduceTypeDesc1.Reduce,
                    new ExpressionToVarRefVisitor(GetSourceRef, cHelper, ReduceTypeDesc1)
                )
                    .Visit(declarationStatement, writeHelper);
                return;
            }

            throw new CompilerException(GetSourceRef(statement), "Unsupported statement");
        }

        private void WriteStatementJumpStatement(CPP14Parser.JumpStatementContext jumpStatement, WriteHelper writeHelper)
        {
            var codeSeg = writeHelper.CodeSeg;

            if (false)
            { }
            else if (jumpStatement.Return() != null)
            {
                if (jumpStatement.expression() is var expression
                    && expression != null)
                {
                    // return (expression);
                    if (writeHelper.ExpectedOuts == 0)
                    {
                        throw new CompilerException(GetSourceRef(jumpStatement), "Function does not expect return value");
                    }

                    var cHelper = writeHelper.CHelper;
                    var expr = new ExpressionToVarRefVisitor(GetSourceRef, cHelper, ReduceTypeDesc1)
                        .VisitExpression(expression);

                    if (expr.Getter == null)
                    {
                        throw new CompilerException(GetSourceRef(expression), "Need to produce getter");
                    }

                    codeSeg.WriteLine(expr.Getter);
                }
                else
                {
                    // return;
                    if (writeHelper.ExpectedOuts != 0)
                    {
                        throw new CompilerException(GetSourceRef(jumpStatement), "Function expects return value");
                    }

                    // push nothing
                }

                codeSeg.WriteLine("ret");
                return;
            }

            throw new CompilerException(GetSourceRef(jumpStatement), "Unsupported jump statement");
        }
    }
}
