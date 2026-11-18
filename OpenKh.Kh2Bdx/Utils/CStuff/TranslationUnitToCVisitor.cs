using Antlr4.Runtime;
using OpenKh.Kh2Bdx.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record TranslationUnitToCVisitor(
        Func<ParserRuleContext, SourceRef> GetSourceRef,
        ReduceTypeDesc ReduceTypeDesc1,
        IReadOnlyList<SyscallPragma>? SyscallPragmas = null)
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

            // built-in traps first, then functions defined in this source (which may shadow traps).
            var stringLabels = new Dictionary<string, string>();
            var funcDict = new Dictionary<string, FuncSig>();
            foreach (var trap in BdxTraps.GetTraps())
            {
                funcDict.TryAdd(
                    trap.Name,
                    new FuncSig(
                        Name: trap.Name,
                        RetType: ((trap.Flags & 0x40000000) != 0) ? TypeDesc.Int32 : TypeDesc.Void,
                        ParamTypes: null,
                        NumIns: trap.Flags & 0xFFFF,
                        Syscall: (trap.TableIndex, trap.TrapIndex)
                    )
                );
            }
            // `#pragma defsyscall`: traps that are not in the table, or another number for one that is (with `override`)
            var pendingPrototypes = new Dictionary<string, SyscallPragma>();
            var definedByPragma = new Dictionary<string, SyscallPragma>();
            foreach (var pragma in SyscallPragmas ?? Array.Empty<SyscallPragma>())
            {
                if (funcDict.TryGetValue(pragma.Name, out var known) && known.Syscall is { } bound)
                {
                    if (bound == (pragma.Table, pragma.Index))
                    {
                        continue;
                    }
                    if (!pragma.Override)
                    {
                        throw new CompilerException(
                            pragma.Position,
                            $"'{pragma.Name}' is the trap {bound.Table}, {bound.Index}: add `override` to make it {pragma.Table}, {pragma.Index}"
                        );
                    }
                    funcDict[pragma.Name] = known with { Syscall = (pragma.Table, pragma.Index) };
                    definedByPragma[pragma.Name] = pragma;
                }
                else
                {
                    // the number of arguments and whether it returns come from the prototype
                    funcDict[pragma.Name] = new FuncSig(pragma.Name, TypeDesc.Void, null, 0, (pragma.Table, pragma.Index));
                    pendingPrototypes[pragma.Name] = pragma;
                    definedByPragma[pragma.Name] = pragma;
                }
            }

            foreach (var declaration in declarationseq.declaration())
            {
                // struct definitions must be known before signatures that use them
                if (declaration.blockDeclaration()?.simpleDeclaration() is { } simpleDeclaration
                    && simpleDeclaration.declSpecifierSeq() is { } specs)
                {
                    ReduceTypeDesc1.Reduce(specs);

                    // and so must typedef names
                    new ParameterDeclarationToNameAndTypePairVisitor(ReduceTypeDesc1.Reduce, GetSourceRef, ReduceTypeDesc1)
                        .TryDeclareTypedefs(simpleDeclaration);

                    // and the prototypes of the traps: they give the types of the arguments and of the result
                    foreach (var initDeclarator in simpleDeclaration.initDeclaratorList()?.initDeclarator() ?? Array.Empty<CPP14Parser.InitDeclaratorContext>())
                    {
                        if (initDeclarator.declarator() is { } prototype
                            && ParameterDeclarationToNameAndTypePairVisitor.IsFunctionDeclarator(prototype)
                            && !ReduceTypeDesc1.Reduce(specs).Typedef)
                        {
                            DeclareSyscallPrototype(prototype, ReduceTypeDesc1.Reduce(specs).TypeDesc, funcDict, pendingPrototypes);
                        }
                    }
                }

                if (declaration.functionDefinition() is var fd && fd != null && fd.functionBody() != null)
                {
                    var (name, retType, argPairs) = GetSignature(fd);
                    if (definedByPragma.TryGetValue(name, out var taken))
                    {
                        throw new CompilerException(GetSourceRef(fd), $"'{name}' is the trap {taken.Table}, {taken.Index} (#pragma defsyscall): it cannot be defined");
                    }
                    funcDict[name] = new FuncSig(
                        Name: name,
                        RetType: retType,
                        ParamTypes: argPairs.Select(it => it.Type).ToArray(),
                        NumIns: argPairs.Length
                    );
                }
            }

            foreach (var missing in pendingPrototypes.Values)
            {
                throw new CompilerException(
                    missing.Position,
                    $"'{missing.Name}' needs a prototype: #pragma defsyscall gives the number of the trap, the prototype its arguments and its result"
                );
            }

            var cHelper = new CHelper(
                GetFunc: name => funcDict.TryGetValue(name, out var sig) ? sig : null,
                AddStringLiteral: bytes =>
                {
                    var key = Convert.ToHexString(bytes);
                    if (!stringLabels.TryGetValue(key, out var label))
                    {
                        label = $"__str{stringLabels.Count + 1}";
                        stringLabels[key] = label;
                        staticDataSeg.WriteLine($"{label}:");
                        staticDataSeg.WriteLine($" db {FormatDbOperands(bytes)}");
                    }
                    return label;
                },
                GetVar: varName => globalVarRefDict.TryGetValue(varName, out VarRef? expr)
                    ? expr
                    : null,
                DeclareVar: (argPair, sourceRef) =>
                {
                    if ((argPair.Type.IsStruct && argPair.Type.PointerDepth == 0) || argPair.Type.IsArray)
                    {
                        // every member / element gets its own label (s.a, s.inner.b, a.0, a.1, ...) at consecutive addresses
                        WriteLeafLabels(bssSeg, argPair.Type, argPair.Name, "resb 4");
                        return globalVarRefDict[argPair.Name] = StructVar.Build(
                            argPair.Type,
                            0,
                            argPair.Name,
                            getter: (_, path) => $"push.d.wp {path}",
                            setter: (_, path) => $"pop.wp {path}",
                            pointer: (_, path) => $"push.wp {path}"
                        );
                    }
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
                    bssSeg.WriteLine($" resb {numBytes}");
                    return var1;
                },
                DeclareStaticVar: (argPair, sourceRef) =>
                {
                    if ((argPair.Type.IsStruct && argPair.Type.PointerDepth == 0) || argPair.Type.IsArray)
                    {
                        WriteLeafLabels(staticDataSeg, argPair.Type, argPair.Name, "resw 2");
                        return globalVarRefDict[argPair.Name] = StructVar.Build(
                            argPair.Type,
                            0,
                            argPair.Name,
                            getter: (_, path) => $"push.d.bd {path}",
                            setter: (_, path) => $"pop.bd {path}",
                            pointer: (_, path) => $"push.bd {path}"
                        );
                    }
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

        /// <summary>
        /// bytes + NUL terminator (+ padding to 2 bytes alignment) as operands of db.
        /// Printable ASCII is quoted (bdscript has no escape in quotes), others are numbers.
        /// </summary>
        private static string FormatDbOperands(byte[] bytes)
        {
            var all = bytes.Concat(new byte[] { 0 }).ToList();
            if (all.Count % 2 != 0)
            {
                all.Add(0);
            }

            var parts = new List<string>();
            var run = new StringBuilder();
            void Flush()
            {
                if (run.Length != 0)
                {
                    parts.Add($"'{run}'");
                    run.Clear();
                }
            }
            foreach (var b in all)
            {
                if (32 <= b && b < 127 && b != '\'')
                {
                    run.Append((char)b);
                }
                else
                {
                    Flush();
                    parts.Add(b.ToString());
                }
            }
            Flush();
            return string.Join(",", parts);
        }

        /// <summary>Emit a label per member (s.a, s.inner.b) and reserve a slot for each scalar leaf.</summary>
        /// <summary>
        /// A label for the variable, then one for each member (s.a, s.inner.b) and element (a.0, a.1, m.1.2) of it
        /// at consecutive addresses. Every scalar reserves <paramref name="reserve"/>.
        /// </summary>
        private static void WriteLeafLabels(TextWriter writer, TypeDesc type, string path, string reserve)
        {
            writer.WriteLine($"{path}:");

            if (type.IsPackedArray)
            {
                // the bytes are packed: a label and a slot for each 4 of them
                for (int x = 0; x < StructVar.SlotsOf(type); x++)
                {
                    writer.WriteLine($"{path}.{x}:");
                    writer.WriteLine($" {reserve}");
                }
            }
            else if (type.IsArray)
            {
                for (int x = 0; x < type.ArrayLength; x++)
                {
                    WriteLeafLabels(writer, type.ElementType!, $"{path}.{x}", reserve);
                }
            }
            else if (type.IsStruct && type.PointerDepth == 0)
            {
                foreach (var member in type.Members!)
                {
                    WriteLeafLabels(writer, member.Type, path + "." + member.Name, reserve);
                }
            }
            else
            {
                writer.WriteLine($" {reserve}");
            }
        }

        /// <summary>
        /// The prototype of a trap gives the types of its arguments and of its result. The number of arguments and whether it
        /// returns must be those of the trap table (they come from the engine; docs are not always right).
        /// A prototype of anything else is ignored.
        /// </summary>
        private void DeclareSyscallPrototype(
            CPP14Parser.DeclaratorContext prototype,
            TypeDesc baseType,
            Dictionary<string, FuncSig> funcDict,
            Dictionary<string, SyscallPragma> pendingPrototypes)
        {
            var (name, ret, parameters) = new ParameterDeclarationToNameAndTypePairVisitor(ReduceTypeDesc1.Reduce, GetSourceRef, ReduceTypeDesc1)
                .ReducePrototype(prototype, baseType);

            if (!funcDict.TryGetValue(name, out var sig) || sig.Syscall == null)
            {
                return;
            }

            static bool IsStructValue(TypeDesc t) => t.IsStruct && t.PointerDepth == 0;
            if (IsStructValue(ret) || parameters.Any(IsStructValue))
            {
                throw new CompilerException(GetSourceRef(prototype), $"A trap cannot take or return a struct by value ('{name}'): use a pointer");
            }

            if (!pendingPrototypes.ContainsKey(name))
            {
                if (parameters.Count != sig.NumIns)
                {
                    throw new CompilerException(
                        GetSourceRef(prototype),
                        $"'{name}' takes {sig.NumIns} argument(s) in the trap table, but the prototype has {parameters.Count}"
                    );
                }
                if ((ret != TypeDesc.Void) != (sig.RetType != TypeDesc.Void))
                {
                    throw new CompilerException(
                        GetSourceRef(prototype),
                        $"'{name}' {(sig.RetType != TypeDesc.Void ? "returns a value" : "does not return a value")} in the trap table, but the prototype says the opposite"
                    );
                }
            }

            funcDict[name] = sig with { RetType = ret, ParamTypes = parameters.ToArray(), NumIns = parameters.Count };
            pendingPrototypes.Remove(name);
        }

        private (string Name, TypeDesc RetType, ArgPair[] Args) GetSignature(
            CPP14Parser.FunctionDefinitionContext functionDefinition)
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
                GetSourceRef,
                ReduceTypeDesc1
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

            return (funcName, retType, argPairs);
        }

        private void VisitFunctionDefinition(
            CPP14Parser.FunctionDefinitionContext functionDefinition,
            TextWriter finalCodeSeg,
            TextWriter initCodeSeg,
            CHelper cHelper)
        {
            var (funcName, retType, argPairs) = GetSignature(functionDefinition);

            // the body is buffered, because jal needs the frame size, which is known only after the whole body is compiled.
            var codeSeg = new StringWriter();

            var frame = new FrameState();

            var argDict = new Dictionary<string, VarRef>();

            // how the callee takes its arguments from tp, in order: a scalar is `pop.sp`, a struct is the address of
            // the caller's struct, which `memcpy.sp` copies into the frame
            var prologue = new List<string>();

            foreach (var argPair in argPairs)
            {
                var spIndex = frame.Top;
                var size = argPair.Type.Size;

                if (argPair.Type.IsStruct && argPair.Type.PointerDepth == 0)
                {
                    // the caller pushed its address; the prologue copies the struct to sp[spIndex...]
                    prologue.Add($"memcpy.sp {argPair.Type.Size}, {4 * spIndex} ; {argPair.Name}");
                    argDict[argPair.Name] = StructVar.Build(
                        argPair.Type,
                        4 * spIndex,
                        argPair.Name,
                        getter: (off, path) => $"push.d.sp {off} ; {path}",
                        setter: (off, path) => $"pop.sp {off} ; {path}",
                        pointer: (off, path) => $"push.sp {off} ; &{path}"
                    );
                    frame.Allocate(StructVar.SlotsOf(argPair.Type));
                    continue;
                }

                argDict[argPair.Name] = new VarRef(
                    Type: argPair.Type,
                    Getter: (size <= 4) ? $"push.d.sp {4 * spIndex} ; {argPair.Name}" : null,
                    Setter: (size <= 4) ? $"pop.sp {4 * spIndex} ; {argPair.Name}" : null,
                    GetPointer: $"push.sp {4 * spIndex} ; &{argPair.Name}"
                );

                prologue.Add($"pop.sp {4 * spIndex}");
                frame.Allocate((size == 0) ? 4 : (size + 3) / 4);
            }

            var numArgItemsUsed = frame.Top;

            if (256 <= numArgItemsUsed)
            {
                throw new CompilerException(GetSourceRef(functionDefinition), funcName + " has too many arguments");
            }

            if (functionDefinition.functionBody() is var functionBody
                && functionBody != null)
            {
                codeSeg.WriteLine($"{funcName}: ; func {funcName}");

                foreach (var line in prologue)
                {
                    codeSeg.WriteLine(line);
                }

                int labelIndex = 0;
                Func<Func<string, string>> nextLabels = () =>
                {
                    labelIndex += 1;
                    var index = labelIndex;
                    return purpose => $"__{funcName}{index}{purpose}";
                };

                cHelper = cHelper with
                {
                    AllocateStackItems = numItems => frame.Allocate(numItems),
                    Frame = frame,
                    NextUniqueLabelGenerator = nextLabels,
                };

                var childScope = NewChildScope(cHelper, argDict);

                WriteStatementsFunctionBody(
                    functionBody,
                    new WriteHelper(
                        CodeSeg: codeSeg,
                        InitCodeSeg: initCodeSeg,
                        ExpectedOuts: (retType != TypeDesc.Void) ? 1 : 0,
                        RetType: retType,
                        NextUniqueLabelGenerator: nextLabels,
                        CHelper: childScope,
                        UserLabels: CollectUserLabels(functionBody, funcName)
                    )
                );
            }

            codeSeg.WriteLine("ret");

            var body = codeSeg.ToString();
            // Hidden slots of stores through a pointer (alive for a few instructions, so no jal covers them).
            foreach (var placeholder in new[] { CHelper.StorePointerSlotPlaceholder, CHelper.StoreValueSlotPlaceholder })
            {
                if (body.Contains(placeholder))
                {
                    body = body.Replace(placeholder, (4 * frame.ReserveAboveHighWater()).ToString());
                }
            }

            // Each jal covers what is alive at that point: locals + live temporaries + 2 system DWORDs (count, ret-ip),
            // aligned to 16 bytes.
            finalCodeSeg.WriteLine(frame.ResolveJalFrameSizes(body, 0));
        }

        /// <summary>
        /// The labels `name:` in a function body, by name. They belong to the whole function (a goto can jump into
        /// and out of blocks), so they are collected before the body is compiled to allow a jump forward.
        /// </summary>
        private Dictionary<string, string> CollectUserLabels(CPP14Parser.FunctionBodyContext functionBody, string funcName)
        {
            var labels = new Dictionary<string, string>();

            void Walk(Antlr4.Runtime.Tree.IParseTree tree)
            {
                if (tree is CPP14Parser.LabeledStatementContext { } labeled && labeled.Identifier() is { } identifier)
                {
                    var name = identifier.GetText();
                    if (!labels.TryAdd(name, $"__{funcName}_L_{name}"))
                    {
                        throw new CompilerException(GetSourceRef(labeled), $"The label '{name}' is defined twice");
                    }
                }

                for (int x = 0; x < tree.ChildCount; x++)
                {
                    Walk(tree.GetChild(x));
                }
            }

            Walk(functionBody);
            return labels;
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
                    var spIndex = parent.AllocateStackItems(StructVar.SlotsOf(argPair.Type));
                    if ((argPair.Type.IsStruct && argPair.Type.PointerDepth == 0) || argPair.Type.IsArray)
                    {
                        return varRefDict[argPair.Name] = StructVar.Build(
                            argPair.Type,
                            4 * spIndex,
                            argPair.Name,
                            getter: (off, path) => $"push.d.sp {off} ; {path}",
                            setter: (off, path) => $"pop.sp {off} ; {path}",
                            pointer: (off, path) => $"push.sp {off} ; &{path}"
                        );
                    }
                    return varRefDict[argPair.Name] = new VarRef(
                        Type: argPair.Type,
                        Getter: (numBytes <= 4) ? $"push.d.sp {4 * spIndex} ; {argPair.Name}" : null,
                        Setter: (numBytes <= 4) ? $"pop.sp {4 * spIndex} ; {argPair.Name}" : null,
                        GetPointer: $"push.sp {4 * spIndex} ; &{argPair.Name}"
                    );
                },
                DeclareStaticVar: parent.DeclareStaticVar,
                AllocateStackItems: parent.AllocateStackItems,
                GetFunc: parent.GetFunc,
                AddStringLiteral: parent.AddStringLiteral,
                Frame: parent.Frame,
                NextUniqueLabelGenerator: parent.NextUniqueLabelGenerator
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
            // temporaries (e.g. copies of returned structs) of a statement are dead when it is done
            var mark = writeHelper.CHelper.TempMark();
            WriteStatementCore(statement, writeHelper);
            writeHelper.CHelper.ReleaseTemps(mark);
        }

        private void WriteStatementCore(CPP14Parser.StatementContext statement, WriteHelper writeHelper)
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
                    var condMark = cHelper.TempMark();
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
                    cHelper.ReleaseTemps(condMark); // the condition is evaluated before the branches run
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
                else if (selectionStatement.Switch() != null)
                {
                    WriteSwitchStatement(selectionStatement, writeHelper);
                    return;
                }
            }

            if (statement.labeledStatement() is { } labeledStatement)
            {
                if (labeledStatement.Identifier() is { } labelName)
                {
                    // `name:` the target of goto
                    codeSeg.WriteLine($"{writeHelper.UserLabels![labelName.GetText()]}:");
                    WriteStatement(labeledStatement.statement(), writeHelper);
                    return;
                }

                // `case X:` / `default:` of the enclosing switch
                if (writeHelper.CaseLabels == null || !writeHelper.CaseLabels.TryGetValue(labeledStatement, out var caseLabel))
                {
                    throw new CompilerException(
                        GetSourceRef(labeledStatement),
                        (labeledStatement.Case() != null || labeledStatement.Default() != null)
                            ? "case/default label outside of a switch"
                            : "Unsupported label"
                    );
                }
                codeSeg.WriteLine($"{caseLabel}:");
                WriteStatement(labeledStatement.statement(), writeHelper);
                return;
            }

            if (statement.iterationStatement() is var iterationStatement
                && iterationStatement != null)
            {
                WriteIterationStatement(iterationStatement, writeHelper);
                return;
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

                    WriteExpressionStatement(expr, writeHelper);
                }
                return;
            }

            if (statement.declarationStatement() is var declarationStatement
                && declarationStatement != null)
            {
                // `f(1);` or `f();` is ambiguous in C++ grammar and is parsed as a declaration with
                // no type at all (a valid declaration needs one). Re-parse it as an expression.
                if (IsDeclarationWithoutType(declarationStatement))
                {
                    WriteReparsedExpressionStatement(statement, writeHelper);
                    return;
                }

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

        /// <summary>
        /// switch (e) { case a: ... default: ... }
        ///
        /// The value is stored once in a hidden local, then compared to each case in order
        /// (`sub` + `beqz`). Bodies keep their source order, so fall-through works as in C.
        /// </summary>
        private void WriteSwitchStatement(CPP14Parser.SelectionStatementContext selectionStatement, WriteHelper writeHelper)
        {
            var codeSeg = writeHelper.CodeSeg;
            var cHelper = writeHelper.CHelper;
            var body = selectionStatement.statement(0);

            var expressionVisitor = new ExpressionToVarRefVisitor(GetSourceRef, cHelper, ReduceTypeDesc1);

            // allocated before the value is visited: temporaries of the value expression are released right after it
            var slot = 4 * cHelper.AllocateStackItems(1);
            var valueMark = cHelper.TempMark();

            var value = expressionVisitor.Visit(selectionStatement.condition());
            if (value.Getter == null)
            {
                throw new CompilerException(GetSourceRef(selectionStatement), "Need to produce getter");
            }
            if (value.Type == TypeDesc.Float32 || value.Type.IsStruct && value.Type.PointerDepth == 0)
            {
                throw new CompilerException(GetSourceRef(selectionStatement), $"switch quantity must be an integer, not {value.Type.Display}");
            }
            var valueConverter = cHelper.GetConverter(value.Type, TypeDesc.Int32)
                ?? throw new CompilerException(GetSourceRef(selectionStatement), $"switch quantity must be an integer, not {value.Type.Display}");

            // labels of this switch only (nested switches own theirs)
            var labeled = new List<CPP14Parser.LabeledStatementContext>();
            void Collect(Antlr4.Runtime.Tree.IParseTree node)
            {
                if (node is CPP14Parser.SelectionStatementContext { } sel && sel.Switch() != null)
                {
                    return;
                }
                if (node is CPP14Parser.LabeledStatementContext l && (l.Case() != null || l.Default() != null))
                {
                    labeled.Add(l);
                }
                for (int x = 0; x < node.ChildCount; x++)
                {
                    Collect(node.GetChild(x));
                }
            }
            Collect(body);

            var labelGen = writeHelper.NextUniqueLabelGenerator();
            var endLabel = labelGen("endswitch");
            var caseLabels = new Dictionary<CPP14Parser.LabeledStatementContext, string>();
            string? defaultLabel = null;
            var seen = new HashSet<string>();

            codeSeg.WriteLine(cHelper.JoinLines(value.Getter, valueConverter, $"pop.sp {slot} ; switch value"));
            cHelper.ReleaseTemps(valueMark);

            for (int x = 0; x < labeled.Count; x++)
            {
                var l = labeled[x];
                if (l.Default() != null)
                {
                    if (defaultLabel != null)
                    {
                        throw new CompilerException(GetSourceRef(l), "Duplicate default label");
                    }
                    defaultLabel = caseLabels[l] = labelGen("default");
                    continue;
                }

                var caseValue = expressionVisitor.VisitConstantExpression(l.constantExpression());
                if (caseValue.Getter == null || caseValue.Type == TypeDesc.Float32)
                {
                    throw new CompilerException(GetSourceRef(l), "case label must be an integer constant");
                }
                var caseConverter = cHelper.GetConverter(caseValue.Type, TypeDesc.Int32)
                    ?? throw new CompilerException(GetSourceRef(l), "case label must be an integer constant");
                var caseCode = cHelper.JoinLines(caseValue.Getter, caseConverter);
                if (!seen.Add(caseCode!))
                {
                    throw new CompilerException(GetSourceRef(l), "Duplicate case value");
                }

                var label = caseLabels[l] = labelGen($"case{x}");
                codeSeg.WriteLine(cHelper.JoinLines($"push.d.sp {slot} ; switch value", caseCode, "sub", $"beqz {label}"));
            }

            codeSeg.WriteLine($"b {defaultLabel ?? endLabel}");

            WriteStatement(
                body,
                writeHelper with
                {
                    CaseLabels = caseLabels,
                    // break leaves the switch; continue still belongs to the enclosing loop
                    Loop = new LoopLabels(Break: endLabel, Continue: writeHelper.Loop?.Continue),
                }
            );

            codeSeg.WriteLine($"{endLabel}:");
        }

        private void WriteReparsedExpressionStatement(ParserRuleContext context, WriteHelper writeHelper)
        {
            var text = context.Start.InputStream.GetText(
                new Antlr4.Runtime.Misc.Interval(context.Start.StartIndex, context.Stop.StopIndex)
            );
            var stream = CharStreams.fromString(text.TrimEnd().TrimEnd(';'));
            ((CodePointCharStream)stream).name = context.Start.InputStream.SourceName;

            // the tokens of the statement keep the line and column they have in the text, for the errors
            if (LineMap.For(context.Start.InputStream) is { } lineMap)
            {
                LineMap.Attach(stream, lineMap);
            }
            var lexer = new CPP14Lexer(stream, TextWriter.Null, TextWriter.Null);
            lexer.Line = context.Start.Line;
            lexer.Column = context.Start.Column;

            var parser = new CPP14Parser(
                new CommonTokenStream(lexer),
                TextWriter.Null,
                TextWriter.Null
            );
            WriteExpressionStatement(
                new ExpressionToVarRefVisitor(GetSourceRef, writeHelper.CHelper, ReduceTypeDesc1)
                    .VisitExpression(parser.expression()),
                writeHelper
            );
        }

        /// <summary>Evaluate and discard.</summary>
        private static void WriteExpressionStatement(VarRef expr, WriteHelper writeHelper)
        {
            if (expr.Getter != null && CHelper.WithoutStoreResult(expr.Getter) is { } storeOnly)
            {
                // a store through a pointer whose value is not used: `address; value; memcpy`
                writeHelper.CodeSeg.WriteLine(storeOnly);
                return;
            }

            if (expr.EffectOnly != null)
            {
                // an assignment or ++/-- whose value is not used
                writeHelper.CodeSeg.WriteLine(expr.EffectOnly);
                return;
            }

            if (expr.DiscardCode != null)
            {
                // unused struct result: the call leaves a pointer on tp, which is dropped
                writeHelper.CodeSeg.WriteLine(expr.DiscardCode);
                writeHelper.CodeSeg.WriteLine("drop");
                return;
            }

            if (expr.Getter != null)
            {
                writeHelper.CodeSeg.WriteLine(expr.Getter);
                if (expr.Type != TypeDesc.Void)
                {
                    writeHelper.CodeSeg.WriteLine("drop");
                }
            }
        }

        private void WriteIterationStatement(CPP14Parser.IterationStatementContext it, WriteHelper writeHelper)
        {
            var codeSeg = writeHelper.CodeSeg;
            var labelGen = writeHelper.NextUniqueLabelGenerator();
            var topLabel = labelGen("loop");
            var contLabel = labelGen("continue");
            var endLabel = labelGen("endloop");
            var inner = writeHelper with { Loop = new LoopLabels(Break: endLabel, Continue: contLabel) };

            string Cond(CPP14Parser.ConditionContext condition, WriteHelper wh)
            {
                var mark = wh.CHelper.TempMark();
                var expr = new ExpressionToVarRefVisitor(GetSourceRef, wh.CHelper, ReduceTypeDesc1).Visit(condition);
                var getter = expr.Getter ?? throw new CompilerException(GetSourceRef(condition), "Need to produce getter");
                wh.CHelper.ReleaseTemps(mark); // the condition is evaluated before the body runs
                return getter;
            }

            if (it.Do() == null && it.While() != null)
            {
                // while (cond) body
                codeSeg.WriteLine($"{topLabel}:");
                codeSeg.WriteLine($"{contLabel}:");
                codeSeg.WriteLine(Cond(it.condition(), writeHelper));
                codeSeg.WriteLine($"jz {endLabel}");
                WriteStatement(it.statement(), inner);
                codeSeg.WriteLine($"b {topLabel}");
                codeSeg.WriteLine($"{endLabel}:");
                return;
            }

            if (it.Do() != null)
            {
                // do body while (cond);
                codeSeg.WriteLine($"{topLabel}:");
                WriteStatement(it.statement(), inner);
                codeSeg.WriteLine($"{contLabel}:");
                var whileMark = writeHelper.CHelper.TempMark();
                var expr = new ExpressionToVarRefVisitor(GetSourceRef, writeHelper.CHelper, ReduceTypeDesc1)
                    .VisitExpression(it.expression());
                codeSeg.WriteLine(expr.Getter ?? throw new CompilerException(GetSourceRef(it.expression()), "Need to produce getter"));
                writeHelper.CHelper.ReleaseTemps(whileMark);
                codeSeg.WriteLine($"jnz {topLabel}");
                codeSeg.WriteLine($"{endLabel}:");
                return;
            }

            if (it.For() != null && it.forInitStatement() is var forInit && forInit != null)
            {
                // for (init; cond; step) body ; variables declared in init live in their own scope
                var scoped = writeHelper with { CHelper = NewChildScope(writeHelper.CHelper) };
                inner = inner with { CHelper = scoped.CHelper };

                var initMark = scoped.CHelper.TempMark();

                if (forInit.simpleDeclaration() is var simpleDeclaration && simpleDeclaration != null)
                {
                    if (simpleDeclaration.declSpecifierSeq() == null && simpleDeclaration.initDeclaratorList() == null)
                    {
                        // empty init
                    }
                    else if (simpleDeclaration.declSpecifierSeq() == null)
                    {
                        WriteReparsedExpressionStatement(simpleDeclaration, scoped);
                    }
                    else
                    {
                        new DeclarationStatementForAssignmentVisitor(
                            GetSourceRef,
                            scoped.CHelper,
                            ReduceTypeDesc1.Reduce,
                            new ExpressionToVarRefVisitor(GetSourceRef, scoped.CHelper, ReduceTypeDesc1)
                        )
                            .VisitSimpleDeclaration(simpleDeclaration, scoped);
                    }
                }
                else if (forInit.expressionStatement()?.expression() is var initExpression && initExpression != null)
                {
                    WriteExpressionStatement(
                        new ExpressionToVarRefVisitor(GetSourceRef, scoped.CHelper, ReduceTypeDesc1).VisitExpression(initExpression),
                        scoped
                    );
                }

                scoped.CHelper.ReleaseTemps(initMark);

                codeSeg.WriteLine($"{topLabel}:");
                if (it.condition() is var condition && condition != null)
                {
                    codeSeg.WriteLine(Cond(condition, scoped));
                    codeSeg.WriteLine($"jz {endLabel}");
                }
                WriteStatement(it.statement(), inner);
                codeSeg.WriteLine($"{contLabel}:");
                if (it.expression() is var step && step != null)
                {
                    var stepMark = scoped.CHelper.TempMark();
                    WriteExpressionStatement(
                        new ExpressionToVarRefVisitor(GetSourceRef, scoped.CHelper, ReduceTypeDesc1).VisitExpression(step),
                        scoped
                    );
                    scoped.CHelper.ReleaseTemps(stepMark);
                }
                codeSeg.WriteLine($"b {topLabel}");
                codeSeg.WriteLine($"{endLabel}:");
                return;
            }

            throw new CompilerException(GetSourceRef(it), "Unsupported iteration statement");
        }

        private bool IsDeclarationWithoutType(CPP14Parser.DeclarationStatementContext declarationStatement)
        {
            var simpleDeclaration = declarationStatement.blockDeclaration()?.simpleDeclaration();
            if (simpleDeclaration == null || simpleDeclaration.initDeclaratorList() == null)
            {
                return false;
            }

            var declSpecifierSeq = simpleDeclaration.declSpecifierSeq();
            if (declSpecifierSeq == null)
            {
                return true;
            }

            // `g(&s);` is parsed as a declaration whose type name is `g`. If it is not a struct tag or a typedef name, it is an expression.
            var typeName = (declSpecifierSeq.declSpecifier().Length == 1)
                ? declSpecifierSeq.declSpecifier()[0].typeSpecifier()?.trailingTypeSpecifier()?.simpleTypeSpecifier()?.theTypeName()?.className()?.Identifier()?.GetText()
                : null;
            return typeName != null && !ReduceTypeDesc1.Structs.ContainsKey(typeName) && !ReduceTypeDesc1.Typedefs.ContainsKey(typeName);
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

                    var retType = writeHelper.RetType;
                    var retIsStruct = retType is { IsStruct: true, PointerDepth: 0 };
                    var exprIsStruct = expr.Type is { IsStruct: true, PointerDepth: 0 };
                    if (retIsStruct || exprIsStruct)
                    {
                        if (!retIsStruct || !exprIsStruct || retType!.BaseName != expr.Type.BaseName)
                        {
                            throw new CompilerException(GetSourceRef(expression), $"Cannot return {expr.Type.Display} from a function returning {retType?.Display}");
                        }

                        // A struct is returned as one pointer on tp, pointing into this (soon dead) frame.
                        if (expr.DiscardCode != null)
                        {
                            // `return g();` the pointer of the callee's dead frame is passed through
                            codeSeg.WriteLine(expr.DiscardCode);
                        }
                        else if (expr.Prelude == null
                            && expr.GetPointer is { } ptr
                            && System.Text.RegularExpressions.Regex.IsMatch(ptr, @"^push\.sp \d+( ;.*)?$"))
                        {
                            // a local or a parameter already lives in this frame
                            codeSeg.WriteLine(ptr);
                        }
                        else
                        {
                            // anything else (global, *p, ...) is copied into a hidden local first
                            var slots = StructVar.SlotsOf(retType);
                            var temp = 4 * cHelper.AllocateTemp(slots);
                            var tempVar = StructVar.Build(
                                retType,
                                temp,
                                "(return value)",
                                getter: (off, path) => $"push.d.sp {off} ; {path}",
                                setter: (off, path) => $"pop.sp {off} ; {path}",
                                pointer: (off, path) => $"push.sp {off} ; &{path}"
                            );
                            codeSeg.WriteLine(cHelper.CopyStruct(tempVar, expr, GetSourceRef(expression)));
                            codeSeg.WriteLine($"push.sp {temp} ; (return value)");
                        }

                        codeSeg.WriteLine("ret");
                        return;
                    }

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

            else if (jumpStatement.Break() != null || jumpStatement.Continue() != null)
            {
                var isBreak = jumpStatement.Break() != null;
                var loop = writeHelper.Loop
                    ?? throw new CompilerException(GetSourceRef(jumpStatement), isBreak ? "break outside of a loop or switch" : "continue outside of a loop");
                var target = isBreak
                    ? loop.Break
                    : loop.Continue ?? throw new CompilerException(GetSourceRef(jumpStatement), "continue outside of a loop");
                codeSeg.WriteLine($"b {target}");
                return;
            }

            else if (jumpStatement.Goto() != null)
            {
                var name = jumpStatement.Identifier().GetText();
                if (writeHelper.UserLabels == null || !writeHelper.UserLabels.TryGetValue(name, out var target))
                {
                    throw new CompilerException(GetSourceRef(jumpStatement), $"goto to the undefined label '{name}'");
                }
                codeSeg.WriteLine($"b {target}");
                return;
            }

            throw new CompilerException(GetSourceRef(jumpStatement), "Unsupported jump statement");
        }
    }
}
