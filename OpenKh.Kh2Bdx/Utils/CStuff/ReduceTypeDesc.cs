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

        /// <summary>struct tags defined so far (global scope only)</summary>
        internal Dictionary<string, TypeDesc> Structs { get; } = new();

        // the same definition can be reached twice (signature pre-pass and the main pass)
        private readonly Dictionary<CPP14Parser.ClassSpecifierContext, TypeDesc> _classCache = new();

        private TypeDesc ReduceClassSpecifier(CPP14Parser.ClassSpecifierContext classSpecifier)
        {
            if (_classCache.TryGetValue(classSpecifier, out var cached))
            {
                return cached;
            }
            return _classCache[classSpecifier] = ReduceClassSpecifierCore(classSpecifier);
        }

        private TypeDesc ReduceClassSpecifierCore(CPP14Parser.ClassSpecifierContext classSpecifier)
        {
            var classHead = classSpecifier.classHead();
            var classKey = classHead.classKey();

            if (classKey?.Struct() == null)
            {
                throw new CompilerException(GetSourceRef(classHead), "Only struct is supported");
            }

            var tagName = classHead.classHeadName()?.className()?.Identifier()?.GetText();

            if (tagName != null && Structs.ContainsKey(tagName))
            {
                throw new CompilerException(GetSourceRef(classHead), $"struct {tagName} is already defined");
            }

            var baseName = tagName != null ? $"struct {tagName}" : "struct";

            // Pass 1: the tag is visible as an empty type, only to measure the size
            // (so that `struct V { struct V *next; }` can name itself).
            if (tagName != null)
            {
                Structs[tagName] = new TypeDesc(BaseName: baseName, Size: 0, IsStruct: true, Members: new List<StructMember>());
            }
            var totalSlots = LayoutMembers(classSpecifier).Sum(it => StructVar.SlotsOf(it.Type));

            // Pass 2: the real type, whose pointer members refer to the real type.
            var members = new List<StructMember>();
            var structType = new TypeDesc(
                BaseName: baseName,
                Size: 4 * totalSlots,
                IsStruct: true,
                Members: members
            );
            if (tagName != null)
            {
                Structs[tagName] = structType;
            }
            members.AddRange(LayoutMembers(classSpecifier));

            return structType;
        }

        private List<StructMember> LayoutMembers(CPP14Parser.ClassSpecifierContext classSpecifier)
        {
            var members = new List<StructMember>();
            var slot = 0;

            foreach (var memberdeclaration in classSpecifier.memberSpecification()?.memberdeclaration()
                ?? Array.Empty<CPP14Parser.MemberdeclarationContext>())
            {
                if (memberdeclaration.declSpecifierSeq() is var declSpecifierSeq
                    && declSpecifierSeq != null
                    && memberdeclaration.memberDeclaratorList() is var memberDeclaratorList
                    && memberDeclaratorList != null)
                {
                    var memberBaseType = Reduce(declSpecifierSeq).TypeDesc;

                    foreach (var memberDeclarator in memberDeclaratorList.memberDeclarator())
                    {
                        var declarator = memberDeclarator.declarator()
                            ?? throw new CompilerException(GetSourceRef(memberDeclarator), "declarator must exist");

                        var pointerDeclarator = declarator.pointerDeclarator()
                            ?? throw new CompilerException(GetSourceRef(declarator), "Unsupported member declarator");
                        var noPointerDeclarator = pointerDeclarator.noPointerDeclarator();
                        var memberType = memberBaseType;
                        foreach (var _ in pointerDeclarator.pointerOperator())
                        {
                            memberType = memberType.MakePointerType();
                        }

                        // arrays: `int v[3]`, `int m[2][3]` (the last bracket is the innermost element type)
                        var arrayDeclarator = noPointerDeclarator;
                        while (arrayDeclarator?.LeftBracket() != null && arrayDeclarator.noPointerDeclarator() is { } innerDeclarator)
                        {
                            var length = arrayDeclarator.constantExpression() is { } constantExpression
                                ? ConstEval.EvaluateArrayLength(constantExpression, GetSourceRef)
                                : throw new CompilerException(GetSourceRef(declarator), "Array member needs a length");
                            memberType = ArrayTypes.Make(memberType, length, GetSourceRef(declarator));
                            arrayDeclarator = innerDeclarator;
                        }

                        if (arrayDeclarator?.parametersAndQualifiers() != null)
                        {
                            throw new CompilerException(GetSourceRef(declarator), "Function members are not supported");
                        }

                        if (memberType.Size == 0)
                        {
                            throw new CompilerException(GetSourceRef(declarator), "Member must not be void or an incomplete type");
                        }

                        var memberName = NameOfDeclarator(declarator);
                        if (members.Any(it => it.Name == memberName))
                        {
                            throw new CompilerException(GetSourceRef(declarator), $"Duplicate member '{memberName}'");
                        }

                        members.Add(new StructMember(memberName, memberType, slot));
                        slot += StructVar.SlotsOf(memberType);
                    }
                }
                else if (memberdeclaration.declSpecifierSeq() != null || memberdeclaration.memberDeclaratorList() != null)
                {
                    throw new CompilerException(GetSourceRef(memberdeclaration), "Unsupported member declaration");
                }
            }

            return members;
        }

        private TypeDesc ReduceElaboratedTypeSpecifier(CPP14Parser.ElaboratedTypeSpecifierContext elaborated)
        {
            if (elaborated.classKey()?.Struct() != null && elaborated.Identifier() is var identifier && identifier != null)
            {
                return LookupStruct(identifier.GetText(), elaborated);
            }

            throw new CompilerException(GetSourceRef(elaborated), "Unsupported elaborated type specifier");
        }

        private TypeDesc LookupStruct(string name, Antlr4.Runtime.ParserRuleContext context)
        {
            return Structs.TryGetValue(name, out var type)
                ? type
                : throw new CompilerException(GetSourceRef(context), $"Unknown type '{name}'");
        }

        private string NameOfDeclarator(CPP14Parser.DeclaratorContext declarator)
        {
            if (declarator.pointerDeclarator() is var pointerDeclarator
                && pointerDeclarator != null)
            {
                if (pointerDeclarator.noPointerDeclarator() is var noPointerDeclarator
                    && noPointerDeclarator != null)
                {
                    // `v[2][3]`: the name is at the bottom of the bracket chain
                    while (noPointerDeclarator.declaratorid() == null && noPointerDeclarator.noPointerDeclarator() is { } inner)
                    {
                        noPointerDeclarator = inner;
                    }

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

            if (trailingTypeSpecifier.elaboratedTypeSpecifier() is var elaborated
                && elaborated != null)
            {
                return ReduceElaboratedTypeSpecifier(elaborated);
            }

            throw new CompilerException(GetSourceRef(trailingTypeSpecifier), "Unsupported trailing type specifier");
        }

        private TypeDesc ReduceSimpleTypeSpecifier(CPP14Parser.SimpleTypeSpecifierContext simpleTypeSpecifier)
        {
            if (simpleTypeSpecifier.theTypeName()?.className()?.Identifier() is var typeIdentifier
                && typeIdentifier != null)
            {
                // `S x;` (struct tag without the struct keyword)
                return LookupStruct(typeIdentifier.GetText(), simpleTypeSpecifier);
            }

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
