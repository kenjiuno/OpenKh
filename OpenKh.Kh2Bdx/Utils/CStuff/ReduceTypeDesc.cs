using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record ReduceTypeDesc(
        Func<Antlr4.Runtime.ParserRuleContext, SourceRef> GetSourceRef)
    {
        internal TypeDescAndStorage Reduce(CPP14Parser.DeclSpecifierSeqContext declSpecifierSeq)
        {
            var declSpecifiers = declSpecifierSeq.declSpecifier().AsSpan();
            var isStatic = false;
            while (true)
            {
                if (declSpecifiers.Length != 0)
                {
                    if (declSpecifiers[0].storageClassSpecifier()?.Extern() != null)
                    {
                        declSpecifiers = declSpecifiers.Slice(1);
                        continue;
                    }
                }
                if (declSpecifiers.Length != 0)
                {
                    if (declSpecifiers[0].storageClassSpecifier()?.Static() != null)
                    {
                        declSpecifiers = declSpecifiers.Slice(1);
                        isStatic = true;
                        continue;
                    }
                }
                break;
            }
            if (false)
            { }
            else if (declSpecifiers.Length == 1
                && declSpecifiers[0]?.typeSpecifier() is var typeSpecifier0
                && typeSpecifier0 != null)
            {
                return new TypeDescAndStorage(ReduceTypeSpecifier1(typeSpecifier0), isStatic);
            }
            else if (declSpecifiers.Length == 2
                && declSpecifiers[0]?.typeSpecifier() is var typeSpecifierW0
                && typeSpecifierW0 != null
                && declSpecifiers[1]?.typeSpecifier() is var typeSpecifierW1
                && typeSpecifierW1 != null)
            {
                return new TypeDescAndStorage(ReduceTypeSpecifier2(typeSpecifierW0, typeSpecifierW1), isStatic);
            }
            else
            {
                throw new CompilerException(GetSourceRef(declSpecifierSeq), "Unsupported declaration specifier sequence");
            }
        }

        internal TypeDesc ReduceTypeSpecifier2(CPP14Parser.TypeSpecifierContext typeSpecifier0, CPP14Parser.TypeSpecifierContext typeSpecifier1)
        {
            var simpleTypeSpecifier0 = typeSpecifier0.trailingTypeSpecifier()?.simpleTypeSpecifier();
            var simpleTypeSpecifier1 = typeSpecifier1.trailingTypeSpecifier()?.simpleTypeSpecifier();

            if (false)
            { }
            else if (simpleTypeSpecifier0 == null || simpleTypeSpecifier1 == null)
            {
                throw new CompilerException(GetSourceRef(typeSpecifier0), "Only simple type specifiers are supported");
            }
            else if (simpleTypeSpecifier0.Signed() != null && simpleTypeSpecifier1.Long() != null)
            {
                return TypeDesc.Int32;
            }
            else if (simpleTypeSpecifier0.Unsigned() != null && simpleTypeSpecifier1.Long() != null)
            {
                return TypeDesc.UInt32;
            }
            else if (simpleTypeSpecifier0.Signed() != null && simpleTypeSpecifier1.Int() != null)
            {
                return TypeDesc.Int32;
            }
            else if (simpleTypeSpecifier0.Unsigned() != null && simpleTypeSpecifier1.Int() != null)
            {
                return TypeDesc.UInt32;
            }
            else if (simpleTypeSpecifier0.Signed() != null && simpleTypeSpecifier1.Short() != null)
            {
                return TypeDesc.Int16;
            }
            else if (simpleTypeSpecifier0.Unsigned() != null && simpleTypeSpecifier1.Short() != null)
            {
                return TypeDesc.UInt16;
            }
            else if (simpleTypeSpecifier0.Signed() != null && simpleTypeSpecifier1.Char() != null)
            {
                return TypeDesc.Int8;
            }
            else if (simpleTypeSpecifier0.Unsigned() != null && simpleTypeSpecifier1.Char() != null)
            {
                return TypeDesc.UInt8;
            }
            else
            {
                throw new CompilerException(GetSourceRef(typeSpecifier0), "Unsupported simple type specifier");
            }
        }

        internal TypeDesc ReduceTypeSpecifier1(CPP14Parser.TypeSpecifierContext typeSpecifier)
        {
            if (typeSpecifier.trailingTypeSpecifier() is var trailingTypeSpecifier
                && trailingTypeSpecifier != null)
            {
                return ReduceTrailingTypeSpecifier(trailingTypeSpecifier);
            }

            if (typeSpecifier.classSpecifier() is var classSpecifier
                && classSpecifier != null)
            {
                // struct / class
                return ReduceClassSpecifier(classSpecifier);
            }

            throw new CompilerException(GetSourceRef(typeSpecifier), "Unsupported type specifier");
        }

        private TypeDesc ReduceClassSpecifier(CPP14Parser.ClassSpecifierContext classSpecifier)
        {
            var classHead = classSpecifier.classHead();

            var isUnion = false;

            {
                var classKey = classHead.classKey();

                if (classKey != null)
                {
                    if (false)
                    { }
                    else if (classKey.Class() != null)
                    {
                        throw new CompilerException(GetSourceRef(classKey), "Class types are not supported");
                    }
                    else if (classKey.Struct() != null)
                    {
                        // expected
                    }
                }
                else if (classHead.Union() != null)
                {
                    // expected
                    isUnion = true;
                }
                else
                {
                    throw new CompilerException(GetSourceRef(classHead), "Unsupported class key");
                }
            }

            if (classHead.classHeadName() is var classHeadName
                && classHeadName != null)
            {
                // struct <classHeadName> { ... };
            }

            var memberSpecification = classSpecifier.memberSpecification();
            if (memberSpecification != null)
            {
                var memberdeclarations = memberSpecification.memberdeclaration();
                if (memberdeclarations != null)
                {
                    int offset = 0;

                    foreach (var memberdeclaration in memberdeclarations)
                    {
                        if (memberdeclaration.declSpecifierSeq() is var declSpecifierSeq
                            && declSpecifierSeq != null
                            && memberdeclaration.memberDeclaratorList() is var memberDeclaratorList
                            && memberDeclaratorList != null)
                        {
                            var memberType = Reduce(declSpecifierSeq);

                            foreach (var memberDeclarator in memberDeclaratorList.memberDeclarator())
                            {
                                var declarator = memberDeclarator.declarator()
                                    ?? throw new CompilerException(GetSourceRef(memberDeclarator), "declarator must exist");

                                var memberName = NameOfDeclarator(declarator);
                            }
                        }
                    }
                }
            }

            throw new NotImplementedException();
        }

        private string NameOfDeclarator(CPP14Parser.DeclaratorContext declarator)
        {
            if (declarator.pointerDeclarator() is var pointerDeclarator
                && pointerDeclarator != null)
            {
                if (pointerDeclarator.noPointerDeclarator() is var noPointerDeclarator
                    && noPointerDeclarator != null)
                {
                    if (noPointerDeclarator.declaratorid() is var declaratorid
                        && declaratorid != null)
                    {
                        if (declaratorid.idExpression() is var idExpression
                            && idExpression != null)
                        {
                            if (idExpression.unqualifiedId() is var unqualifiedId
                                && unqualifiedId != null)
                            {
                                if (unqualifiedId.Identifier() is var identifier
                                    && identifier != null)
                                {
                                    return identifier.GetText();
                                }
                            }
                        }
                    }
                }
            }

            throw new CompilerException(GetSourceRef(declarator), "Unsupported declarator for name extraction");
        }

        private TypeDesc ReduceTrailingTypeSpecifier(CPP14Parser.TrailingTypeSpecifierContext trailingTypeSpecifier)
        {
            if (trailingTypeSpecifier.simpleTypeSpecifier() is var simpleTypeSpecifier
                && simpleTypeSpecifier != null)
            {
                return ReduceSimpleTypeSpecifier(simpleTypeSpecifier);
            }

            throw new CompilerException(GetSourceRef(trailingTypeSpecifier), "Unsupported trailing type specifier");
        }

        private TypeDesc ReduceSimpleTypeSpecifier(CPP14Parser.SimpleTypeSpecifierContext simpleTypeSpecifier)
        {
            if (false)
            { }
            else if (simpleTypeSpecifier.Float() != null)
            {
                return TypeDesc.Float32;
            }
            else if (simpleTypeSpecifier.Signed() != null)
            {
                return TypeDesc.Int32;
            }
            else if (simpleTypeSpecifier.Unsigned() != null)
            {
                return TypeDesc.UInt32;
            }
            else if (simpleTypeSpecifier.Long() != null)
            {
                return TypeDesc.Int32;
            }
            else if (simpleTypeSpecifier.Int() != null)
            {
                return TypeDesc.Int32;
            }
            else if (simpleTypeSpecifier.Short() != null)
            {
                return TypeDesc.Int16;
            }
            else if (simpleTypeSpecifier.Char() != null)
            {
                return TypeDesc.Int8;
            }
            else if (simpleTypeSpecifier.Void() != null)
            {
                return TypeDesc.Void;
            }
            else
            {
                throw new CompilerException(GetSourceRef(simpleTypeSpecifier), "Unsupported simple type specifier");
            }
        }
    }
}
