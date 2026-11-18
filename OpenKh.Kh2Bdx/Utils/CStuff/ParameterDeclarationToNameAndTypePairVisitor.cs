using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record ParameterDeclarationToNameAndTypePairVisitor(
        Func<CPP14Parser.DeclSpecifierSeqContext, TypeDescAndStorage> ReduceTypeDesc2,
        Func<Antlr4.Runtime.ParserRuleContext, SourceRef> GetSourceRef,
        ReduceTypeDesc? Types = null,
        Func<string, VarRef?>? GetVar = null)
    {
        /// <summary>
        /// `typedef T name;`, `typedef struct { ... } V, *PV;`, `typedef int Row[4];`: define the names of the
        /// declarators as types. False if the declaration is not a typedef.
        /// </summary>
        internal bool TryDeclareTypedefs(CPP14Parser.SimpleDeclarationContext simpleDeclaration)
        {
            if (simpleDeclaration.declSpecifierSeq() is not { } specs || Types == null)
            {
                return false;
            }

            var reduced = ReduceTypeDesc2(specs);
            if (!reduced.Typedef)
            {
                return false;
            }

            foreach (var initDeclarator in simpleDeclaration.initDeclaratorList()?.initDeclarator() ?? Array.Empty<CPP14Parser.InitDeclaratorContext>())
            {
                if (initDeclarator.initializer() != null)
                {
                    throw new CompilerException(GetSourceRef(initDeclarator), "A typedef cannot have an initializer");
                }

                var pair = ReduceDeclarator(initDeclarator.declarator(), reduced.TypeDesc);
                Types.Typedefs[pair.Name] = pair.Type;
            }
            return true;
        }

        internal ArgPair Reduce(CPP14Parser.ParameterDeclarationContext parameterDeclaration)
        {
            var baseType = ReduceTypeDesc2(
                parameterDeclaration
                    .declSpecifierSeq() ?? throw new CompilerException(GetSourceRef(parameterDeclaration), "declSpecifierSeq must exist")
            )
                .TypeDesc;

            var parameter = ReduceParameterDeclaration(parameterDeclaration, baseType);

            if (parameter.Type.IsArray)
            {
                // `int a[]` as a parameter is a pointer
                if (parameter.Type.ElementType!.IsArray)
                {
                    throw new CompilerException(GetSourceRef(parameterDeclaration), "Parameters of pointer-to-array type are not supported");
                }
                parameter = parameter with { Type = parameter.Type.Decay() };
            }

            return parameter;
        }

        private ArgPair ReduceParameterDeclaration(CPP14Parser.ParameterDeclarationContext parameterDeclaration, TypeDesc baseType)
        {
            if (parameterDeclaration.declarator() is var declarator
                && declarator != null)
            {
                return ReduceDeclarator(declarator, baseType);
            }

            throw new CompilerException(GetSourceRef(parameterDeclaration), "Unsupported parameter declaration");
        }

        internal ArgPair ReduceDeclarator(CPP14Parser.DeclaratorContext declarator, TypeDesc baseType)
        {
            if (declarator.noPointerDeclarator() is var noPointerDeclarator
                && noPointerDeclarator != null)
            {
                return ReduceNoPointerDeclarator(noPointerDeclarator, baseType);
            }

            if (declarator.pointerDeclarator() is var pointerDeclarator
                && pointerDeclarator != null)
            {
                return ReducePointerDeclarator(pointerDeclarator, baseType);
            }

            throw new CompilerException(GetSourceRef(declarator), "Unsupported declarator");
        }

        private ArgPair ReducePointerDeclarator(CPP14Parser.PointerDeclaratorContext pointerDeclarator, TypeDesc baseType)
        {
            if (pointerDeclarator.pointerOperator() is var pointerOperators
                && pointerOperators != null
                && pointerOperators.Any())
            {
                for (int i = 0; i < pointerOperators.Length; i++)
                {
                    baseType = baseType.MakePointerType();
                }
            }

            if (pointerDeclarator.noPointerDeclarator() is var noPointerDeclarator
                && noPointerDeclarator != null)
            {
                // pointer operators were already applied to baseType above
                return ReduceNoPointerDeclarator(noPointerDeclarator, baseType);
            }

            throw new CompilerException(GetSourceRef(pointerDeclarator), "Unsupported pointer declarator");
        }

        /// <summary>The parameter types of `(int, char *)` or `(int a, int b)`; `(void)` and `()` are none.</summary>
        private List<TypeDesc> ParamTypesOf(CPP14Parser.ParametersAndQualifiersContext parametersAndQualifiers)
        {
            var types = new List<TypeDesc>();
            foreach (var parameterDeclaration in parametersAndQualifiers.parameterDeclarationClause()?.parameterDeclarationList()?.parameterDeclaration()
                ?? Array.Empty<CPP14Parser.ParameterDeclarationContext>())
            {
                var baseType = ReduceTypeDesc2(
                    parameterDeclaration.declSpecifierSeq() ?? throw new CompilerException(GetSourceRef(parameterDeclaration), "declSpecifierSeq must exist")
                ).TypeDesc;

                TypeDesc type;
                if (parameterDeclaration.declarator() is { } declarator)
                {
                    type = ReduceDeclarator(declarator, baseType).Type;
                }
                else
                {
                    // a parameter without a name: `int`, `char *`
                    type = baseType;
                    if (parameterDeclaration.abstractDeclarator() is { } abstractDeclarator)
                    {
                        var pointers = abstractDeclarator.pointerAbstractDeclarator();
                        if (pointers == null || pointers.noPointerAbstractDeclarator() != null || pointers.pointerOperator().Any(it => it.Star() == null))
                        {
                            throw new CompilerException(GetSourceRef(abstractDeclarator), "Unsupported parameter type");
                        }
                        foreach (var _ in pointers.pointerOperator())
                        {
                            type = type.MakePointerType();
                        }
                    }
                }

                types.Add(type.IsArray ? type.Decay() : type);
            }

            if (types.Count == 1 && types[0] == TypeDesc.Void)
            {
                types.Clear();
            }
            return types;
        }

        private ArgPair ReduceNoPointerDeclarator(CPP14Parser.NoPointerDeclaratorContext noPointerDeclarator, TypeDesc baseType)
        {
            if (noPointerDeclarator.parametersAndQualifiers() is { } parametersAndQualifiers
                && noPointerDeclarator.noPointerDeclarator() is { } innerDeclarator
                && innerDeclarator.pointerDeclarator() is { } innerPointerDeclarator)
            {
                // a function pointer: `int (*fp)(int, int)`, `int (*table[3])(int)`. The declarator in the
                // parentheses has the star, and the type to the left is what the function returns.
                var pointerOperators = innerPointerDeclarator.pointerOperator();
                if (pointerOperators.Length == 0 || Types == null)
                {
                    throw new CompilerException(GetSourceRef(noPointerDeclarator), "Unsupported declarator");
                }

                var functionPointer = TypeDesc.FunctionPointer(new FuncType(baseType, ParamTypesOf(parametersAndQualifiers)));
                for (int x = 1; x < pointerOperators.Length; x++)
                {
                    functionPointer = functionPointer.MakePointerType();
                }
                return ReduceNoPointerDeclarator(innerPointerDeclarator.noPointerDeclarator(), functionPointer);
            }

            if (noPointerDeclarator.declaratorid() is var declaratorid
                && declaratorid != null)
            {
                return ReduceDeclaratorid(declaratorid, baseType);
            }

            if (noPointerDeclarator.noPointerDeclarator() is var innerNoPointerDeclarator
                && innerNoPointerDeclarator != null)
            {
                if (noPointerDeclarator.LeftBracket() != null)
                {
                    // `a[3]`, `a[2][3]`, `a[]`: the last bracket is the innermost element type
                    var length = noPointerDeclarator.constantExpression() is { } constantExpression
                        ? ConstEval.EvaluateArrayLength(constantExpression, GetSourceRef, Types, GetVar)
                        : -1;
                    baseType = ArrayTypes.Make(baseType, length, GetSourceRef(noPointerDeclarator));
                }

                return ReduceNoPointerDeclarator(innerNoPointerDeclarator, baseType);
            }

            throw new CompilerException(GetSourceRef(noPointerDeclarator), "Unsupported no pointer declarator");
        }

        private ArgPair ReduceDeclaratorid(CPP14Parser.DeclaratoridContext declaratorid, TypeDesc baseType)
        {
            if (declaratorid.idExpression() is var idExpression
                && idExpression != null)
            {
                return ReduceIdExpression(idExpression, baseType);
            }

            throw new CompilerException(GetSourceRef(declaratorid), "Unsupported declarator id");
        }

        private ArgPair ReduceIdExpression(CPP14Parser.IdExpressionContext idExpression, TypeDesc baseType)
        {
            if (idExpression.unqualifiedId() is var unqualifiedId
                && unqualifiedId != null)
            {
                return ReduceUnqualifiedId(unqualifiedId, baseType);
            }

            throw new CompilerException(GetSourceRef(idExpression), "Unsupported id expression");
        }

        private ArgPair ReduceUnqualifiedId(CPP14Parser.UnqualifiedIdContext unqualifiedId, TypeDesc baseType)
        {
            if (unqualifiedId.Identifier() is var identifier
                && identifier != null)
            {
                return new ArgPair(
                    Type: baseType,
                    Name: identifier.GetText()
                );
            }

            throw new CompilerException(GetSourceRef(unqualifiedId), "Unsupported unqualified id");
        }
    }
}
