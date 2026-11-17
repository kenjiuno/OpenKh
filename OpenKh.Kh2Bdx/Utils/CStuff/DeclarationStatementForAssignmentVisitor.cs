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

        internal void VisitSimpleDeclaration(CPP14Parser.SimpleDeclarationContext simpleDeclaration, WriteHelper writeHelper)
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

            if (varTypePair != null)
            {
                // `struct S { ... };` defines a type only
                return;
            }

            if (simpleDeclaration.GetText() == ";")
            {
                // the empty statement
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

                            if (varPair.Type.IsArray && varPair.Type.ArrayLength < 0)
                            {
                                // `int a[] = { 1, 2, 3 };` the length is the number of the initializers
                                var braced = (initDeclarator.initializer() is { } init) ? GetBracedInitList(init) : null;
                                var literalBytes = (initDeclarator.initializer() is { } init2 && init2.braceOrEqualInitializer()?.initializerClause() is { } clause2)
                                    ? TryGetStringLiteral(clause2)
                                    : null;
                                int count;
                                if (varPair.Type.IsByteArray && literalBytes != null)
                                {
                                    // char s[] = "abc";  the NUL is part of the array
                                    count = literalBytes.Length + 1;
                                }
                                else if (braced != null)
                                {
                                    count = braced.initializerList()?.initializerClause().Length ?? 0;
                                }
                                else
                                {
                                    throw new CompilerException(GetSourceRef(declarator), $"Array '{varPair.Name}' needs a length or a {{ ... }} initializer");
                                }
                                varPair = varPair with
                                {
                                    Type = ArrayTypes.Make(varPair.Type.ElementType!, count, GetSourceRef(declarator)),
                                };
                            }

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
                        if (GetBracedInitList(initializer) is { } bracedInitList)
                        {
                            // struct S s = { 1, 2 };  /  int a = { 1 };
                            if (assignTo == null)
                            {
                                throw new CompilerException(GetSourceRef(initDeclarator), "No lvalue to assign to");
                            }

                            WriteBracedInit(
                                assignTo,
                                bracedInitList,
                                (varTypePair?.Static ?? false)
                                    ? writeHelper.InitCodeSeg
                                    : writeHelper.CodeSeg
                            );
                            continue;
                        }

                        if (assignTo != null
                            && assignTo.Type.IsByteArray
                            && initializer.braceOrEqualInitializer()?.initializerClause() is { } stringClause
                            && TryGetStringLiteral(stringClause) is { } stringBytes)
                        {
                            // char s[8] = "abc";
                            WriteStringInit(
                                assignTo,
                                stringBytes,
                                GetSourceRef(stringClause),
                                (varTypePair?.Static ?? false) ? writeHelper.InitCodeSeg : writeHelper.CodeSeg
                            );
                            continue;
                        }

                        var rval = VisitInitializer(initializer);
                        if (rval != null)
                        {
                            if (assignTo != null && IsStructValue(assignTo.Type))
                            {
                                // struct S b = a;
                                (((varTypePair?.Static ?? false) ? writeHelper.InitCodeSeg : writeHelper.CodeSeg))
                                    .WriteLine(CHelper.CopyStruct(assignTo, rval, GetSourceRef(initDeclarator)));
                                continue;
                            }

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

        private static CPP14Parser.BracedInitListContext? GetBracedInitList(CPP14Parser.InitializerContext initializer)
        {
            var b = initializer.braceOrEqualInitializer();
            return b?.bracedInitList() ?? b?.initializerClause()?.bracedInitList();
        }

        private static bool IsStructValue(TypeDesc type) => type.IsStruct && type.PointerDepth == 0;

        /// <summary>
        /// Initialize target with `{ a, b, ... }`. Elements follow the member order, nested braces initialize
        /// nested structs, and members without an element are set to zero (as in C).
        /// </summary>
        private void WriteBracedInit(VarRef target, CPP14Parser.BracedInitListContext list, TextWriter writeTo)
        {
            var clauses = list.initializerList()?.initializerClause()
                ?? Array.Empty<CPP14Parser.InitializerClauseContext>();

            if (target.Type.IsPackedArray)
            {
                // char s[8] = { 'a', 0 };  short h[4] = { 1, 2 };  constants, the rest are zero
                var bytes = new List<byte>();
                var length = target.Type.ArrayLength;
                if (length < clauses.Length)
                {
                    throw new CompilerException(GetSourceRef(clauses[length]), $"Too many initializers for {target.Type.Display}");
                }
                foreach (var clause in clauses)
                {
                    if (clause.bracedInitList() != null || VisitInitializerClause(clause)?.Getter is not { } code
                        || !ConstEval.TryEvaluateCode(code, out var value))
                    {
                        throw new CompilerException(GetSourceRef(clause), "An element of a char or short array must be a constant");
                    }
                    bytes.Add((byte)value);
                    if (target.Type.ElementType!.IsShort)
                    {
                        bytes.Add((byte)(value >> 8));
                    }
                }
                WriteBytes(target, bytes.ToArray(), writeTo);
                return;
            }

            if (target.Type.IsArray)
            {
                // int a[3] = { 1, 2 };  elements in order, the rest are zero
                var elements = target.Elements ?? throw new CompilerException(GetSourceRef(list), $"Cannot initialize {target.Type.Display}");

                if (elements.Count < clauses.Length)
                {
                    throw new CompilerException(GetSourceRef(clauses[elements.Count]), $"Too many initializers for {target.Type.Display}");
                }

                for (int x = 0; x < elements.Count; x++)
                {
                    if (x < clauses.Length)
                    {
                        WriteInitClause(elements[x], clauses[x], writeTo);
                    }
                    else
                    {
                        WriteZero(elements[x], writeTo);
                    }
                }
                return;
            }

            if (IsStructValue(target.Type))
            {
                var members = target.Type.Members ?? throw new CompilerException(GetSourceRef(list), $"Incomplete type {target.Type.Display}");

                if (members.Count < clauses.Length)
                {
                    throw new CompilerException(GetSourceRef(clauses[members.Count]), $"Too many initializers for {target.Type.Display}");
                }

                for (int x = 0; x < members.Count; x++)
                {
                    var member = target.Members![members[x].Name];
                    if (x < clauses.Length)
                    {
                        WriteInitClause(member, clauses[x], writeTo);
                    }
                    else
                    {
                        WriteZero(member, writeTo);
                    }
                }
                return;
            }

            // scalar: `int a = { 1 };`
            if (1 < clauses.Length)
            {
                throw new CompilerException(GetSourceRef(clauses[1]), "Too many initializers for a scalar");
            }
            if (clauses.Length == 0)
            {
                WriteZero(target, writeTo);
            }
            else
            {
                WriteInitClause(target, clauses[0], writeTo);
            }
        }

        private void WriteInitClause(VarRef target, CPP14Parser.InitializerClauseContext clause, TextWriter writeTo)
        {
            if (clause.bracedInitList() is { } nested)
            {
                WriteBracedInit(target, nested, writeTo);
                return;
            }

            if (target.Type.IsByteArray && TryGetStringLiteral(clause) is { } literal)
            {
                // { "ab", "cd" } for char names[2][8]
                WriteStringInit(target, literal, GetSourceRef(clause), writeTo);
                return;
            }

            if (target.Type.IsArray)
            {
                throw new CompilerException(GetSourceRef(clause), $"Cannot initialize {target.Type.Display} from an expression; use {{ ... }}");
            }

            var rval = VisitInitializerClause(clause)!;

            if (IsStructValue(target.Type))
            {
                // struct member initialized by a struct value of the same type
                writeTo.WriteLine(CHelper.CopyStruct(target, rval, GetSourceRef(clause)));
                return;
            }

            var converter = CHelper.GetConverter(FromType: rval.Type, ToType: target.Type)
                ?? throw new CompilerException(
                    GetSourceRef(clause),
                    $"No converter from {rval.Type.Display} to {target.Type.Display}"
                );
            writeTo.WriteLine(
                CHelper.JoinLines(
                    rval.Getter,
                    converter,
                    target.Setter ?? throw new CompilerException(GetSourceRef(clause), "Lvalue has no setter")
                )
            );
        }

        /// <summary>The bytes of a string literal (adjacent literals are joined), or null if the clause is not one.</summary>
        private byte[]? TryGetStringLiteral(CPP14Parser.InitializerClauseContext clause)
        {
            var text = clause.GetText();
            var matches = System.Text.RegularExpressions.Regex.Matches(text, "\"(?:[^\"\\\\]|\\\\.)*\"");
            if (matches.Count == 0 || matches.Sum(m => m.Length) != text.Length)
            {
                return null;
            }
            var sourceRef = GetSourceRef(clause);
            return matches.SelectMany(m => ExpressionToVarRefVisitor.UnescapeStringLiteral(m.Value, sourceRef)).ToArray();
        }

        /// <summary>char s[N] = "text"; the NUL is included if it fits, and the rest is zero.</summary>
        private void WriteStringInit(VarRef target, byte[] literal, SourceRef sourceRef, TextWriter writeTo)
        {
            if (target.Type.ArrayLength < literal.Length)
            {
                throw new CompilerException(sourceRef, $"String of {literal.Length} bytes does not fit in {target.Type.Display}");
            }
            WriteBytes(target, literal, writeTo);
        }

        /// <summary>Fill the packed bytes of a char array: the given bytes, then zero. A slot is written as a whole.</summary>
        private void WriteBytes(VarRef target, byte[] bytes, TextWriter writeTo)
        {
            var slots = target.Slots ?? throw new InvalidOperationException($"Cannot initialize {target.Type.Display}: no slots");
            for (int x = 0; x < slots.Count; x++)
            {
                var word = 0;
                for (int b = 0; b < 4; b++)
                {
                    var at = 4 * x + b;
                    if (at < bytes.Length)
                    {
                        word |= bytes[at] << (8 * b);
                    }
                }
                writeTo.WriteLine(
                    CHelper.JoinLines(
                        $"push {word}",
                        slots[x].Setter ?? throw new InvalidOperationException($"Cannot write {target.Type.Display}: no setter")
                    )
                );
            }
        }

        private void WriteZero(VarRef target, TextWriter writeTo)
        {
            if (target.Type.IsPackedArray)
            {
                WriteBytes(target, Array.Empty<byte>(), writeTo);
                return;
            }

            if (target.Type.IsArray)
            {
                foreach (var element in target.Elements!)
                {
                    WriteZero(element, writeTo);
                }
                return;
            }

            if (IsStructValue(target.Type))
            {
                foreach (var member in target.Type.Members!)
                {
                    WriteZero(target.Members![member.Name], writeTo);
                }
                return;
            }

            // 0, 0.0f and a null pointer are all the zero word
            writeTo.WriteLine(
                CHelper.JoinLines(
                    "push 0",
                    target.Setter ?? throw new InvalidOperationException($"Cannot zero {target.Type.Display}: no setter")
                )
            );
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

                if (expr.Getter == null && !IsStructValue(expr.Type))
                {
                    throw new CompilerException(GetSourceRef(assignmentExpression), "Need to produce getter");
                }

                return expr;
            }

            throw new CompilerException(GetSourceRef(initializerClause), "Unsupported initializer clause");
        }
    }
}
