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
        Func<Antlr4.Runtime.ParserRuleContext, SourceRef> GetSourceRef)
    {
        internal ArgPair Reduce(CPP14Parser.ParameterDeclarationContext parameterDeclaration)
        {
            var baseType = ReduceTypeDesc2(
                parameterDeclaration
                    .declSpecifierSeq() ?? throw new CompilerException(GetSourceRef(parameterDeclaration), "declSpecifierSeq must exist")
            )
                .TypeDesc;

            return ReduceParameterDeclaration(parameterDeclaration, baseType);
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
                var argType = ReduceNoPointerDeclarator(noPointerDeclarator, baseType);

                var cx = pointerDeclarator.pointerOperator().Length;
                for (int x = 0; x < cx; x++)
                {
                    argType = argType with { Type = argType.Type.MakePointerType() };
                }

                return argType;
            }

            throw new CompilerException(GetSourceRef(pointerDeclarator), "Unsupported pointer declarator");
        }

        private ArgPair ReduceNoPointerDeclarator(CPP14Parser.NoPointerDeclaratorContext noPointerDeclarator, TypeDesc baseType)
        {
            if (noPointerDeclarator.declaratorid() is var declaratorid
                && declaratorid != null)
            {
                return ReduceDeclaratorid(declaratorid, baseType);
            }

            if (noPointerDeclarator.noPointerDeclarator() is var innerNoPointerDeclarator
                && innerNoPointerDeclarator != null)
            {
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
