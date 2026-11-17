using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CPP14Parser;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <summary>
    /// Going to resolve ConditionContext into a VarRef.
    /// </summary>
    internal record ExpressionToVarRefVisitor(
        Func<ParserRuleContext, SourceRef> GetSourceRef,
        CHelper CHelper,
        ReduceTypeDesc ReduceTypeDesc1)
    {
        public VarRef Visit(ConditionContext conditionContext)
        {
            if (conditionContext.expression() is var expression)
            {
                return VisitExpression(expression);
            }

            throw new CompilerException(GetSourceRef(conditionContext), "Unsupported condition context");
        }

        public VarRef VisitConstantExpression(ConstantExpressionContext constantExpression)
        {
            return VisitConditionalExpression(constantExpression.conditionalExpression());
        }

        public VarRef VisitExpression(ExpressionContext expressionContext)
        {
            if (expressionContext.assignmentExpression() is var assignments
                && assignments.Any())
            {
                // must be usage of camma operator at rval.
                var cx = assignments.Length;
                VarRef? lval = null;
                for (int x = 0; x < cx; x++)
                {
                    var rval = VisitAssignment(assignments[x]);
                    if (lval == null)
                    {
                        lval = rval;
                    }
                    else
                    {
                        if (rval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(assignments[x]), "Right value must have a getter");
                        }

                        lval = new VarRef(
                            Type: rval.Type,
                            Getter: CHelper.JoinLines(
                                lval.Getter,
                                "drop",
                                rval.Getter
                            ),
                            Setter: rval.Setter,
                            GetPointer: rval.GetPointer
                        );
                    }
                }
                return lval!;
            }

            throw new CompilerException(GetSourceRef(expressionContext), "Unsupported expression context");
        }

        public VarRef VisitAssignment(AssignmentExpressionContext assignmentExpression)
        {
            if (assignmentExpression.conditionalExpression() is var conditionalExpression
                && conditionalExpression != null)
            {
                return VisitConditionalExpression(conditionalExpression);
            }

            if (assignmentExpression.logicalOrExpression() is var logicalOrExpression
                && logicalOrExpression != null
                && assignmentExpression.initializerClause() is var initializerClause
                && initializerClause != null)
            {
                // The right side is visited first. It runs first, and its temporaries (e.g. a returned struct that
                // is being copied) are alive while a call in the left side (`f()->x = g()`) runs. Frame sizes of
                // calls are decided in visiting order, so the order must not hide a live temporary from them.
                var rval = VisitAssignment(initializerClause.assignmentExpression());
                var lval = VisitLogicalOrExpression(logicalOrExpression);

                var assignmentOperator = assignmentExpression.assignmentOperator();

                if (lval.Type.IsStruct && lval.Type.PointerDepth == 0)
                {
                    // struct assignment: copy member by member. It has no value on tp (type void),
                    // so it can be used as a statement only.
                    if (assignmentOperator != null && assignmentOperator.Assign() == null)
                    {
                        throw new CompilerException(GetSourceRef(assignmentExpression), $"Operator is not supported for {lval.Type.Display}");
                    }

                    return new VarRef(
                        Type: TypeDesc.Void,
                        Getter: CHelper.CopyStruct(lval, rval, GetSourceRef(assignmentExpression))
                    );
                }

                if (lval.Setter == null)
                {
                    throw new CompilerException(GetSourceRef(assignmentExpression), "Left value must have a setter");
                }

                if (rval.Getter == null)
                {
                    throw new CompilerException(GetSourceRef(assignmentExpression), "Right value must have a getter");
                }

                var converter = CHelper.GetConverter(FromType: rval.Type, ToType: lval.Type)
                    ?? throw new CompilerException(GetSourceRef(assignmentExpression), $"Cannot convert from {rval.Type.Display} to {lval.Type.Display}");

                // The value of an assignment expression is the stored value: compute it,
                // `dup` it, and pop one copy into the target. One copy stays on tp.
                // A variable reached through a pointer is stored with `memcpy` like real data does: the address is
                // evaluated first and pushed, then the value.
                VarRef Store(bool compound, string? rvalCode, string? lines)
                {
                    return new VarRef(
                        Type: lval.Type,
                        Getter: lval.Target is { } target
                            ? CHelper.JoinLines(
                                target.Address,
                                compound ? CHelper.JoinLines("dup", target.Load) : null,
                                rvalCode,
                                lines,
                                CHelper.StoreTail(target)
                            )
                            : CHelper.JoinLines(compound ? lval.Getter : null, rvalCode, lines, "dup", lval.Setter),
                        Setter: null
                    );
                }

                if (assignmentOperator == null || assignmentOperator.Assign() != null)
                {
                    return Store(false, rval.Getter, converter);
                }
                else
                {
                    if (lval.Getter == null)
                    {
                        throw new CompilerException(GetSourceRef(assignmentExpression), "Left value must have a getter for compound assignment");
                    }

                    VarRef Apply(string opName, string? lines)
                    {
                        if (lines == null)
                        {
                            throw new CompilerException(
                                GetSourceRef(assignmentOperator),
                                $"Operation {opName} not supported for type {lval.Type.Display}"
                            );
                        }

                        return Store(true, rval.Getter, CHelper.JoinLines(converter, lines));
                    }

                    if (false)
                    { }
                    else if (assignmentOperator.PlusAssign() != null)
                    {
                        return Apply("add", lval.Type.Math?.ApplyAdd);
                    }
                    else if (assignmentOperator.MinusAssign() != null)
                    {
                        return Apply("sub", lval.Type.Math?.ApplySub);
                    }
                    else if (assignmentOperator.StarAssign() != null)
                    {
                        return Apply("mul", lval.Type.Math?.ApplyMul);
                    }
                    else if (assignmentOperator.DivAssign() != null)
                    {
                        return Apply("div", lval.Type.Math?.ApplyDiv);
                    }
                    else if (assignmentOperator.ModAssign() != null)
                    {
                        return Apply("mod", lval.Type.Math?.ApplyMod);
                    }
                    else if (assignmentOperator.AndAssign() != null)
                    {
                        return Apply("and", lval.Type.Math?.ApplyAnd);
                    }
                    else if (assignmentOperator.OrAssign() != null)
                    {
                        return Apply("or", lval.Type.Math?.ApplyOr);
                    }
                    else if (assignmentOperator.XorAssign() != null)
                    {
                        return Apply("xor", lval.Type.Math?.ApplyXor);
                    }
                    else if (assignmentOperator.LeftShiftAssign() != null)
                    {
                        return Apply("sll", lval.Type.Math?.ApplySll);
                    }
                    else if (assignmentOperator.RightShiftAssign() != null)
                    {
                        return Apply("sra", lval.Type.Math?.ApplySra);
                    }
                    else
                    {
                        throw new CompilerException(GetSourceRef(assignmentExpression), "Unsupported assignment operator");
                    }
                }
            }

            throw new CompilerException(GetSourceRef(assignmentExpression), "Unsupported assignment expression");
        }

        private VarRef VisitConditionalExpression(ConditionalExpressionContext conditionalExpression)
        {
            if (conditionalExpression.logicalOrExpression() is var logicalOrExpression
                && logicalOrExpression != null)
            {
                var condition = VisitLogicalOrExpression(logicalOrExpression);
                if (conditionalExpression.Question() == null)
                {
                    return condition;
                }

                // c ? a : b
                //   c; beqz Lelse; a; b Lend; Lelse: b; Lend:
                var sourceRef = GetSourceRef(conditionalExpression);
                var whenTrue = Promote(DecayArray(VisitExpression(conditionalExpression.expression())));
                var whenFalse = Promote(DecayArray(VisitAssignment(conditionalExpression.assignmentExpression())));

                if (condition.Getter == null || whenTrue.Getter == null || whenFalse.Getter == null)
                {
                    throw new CompilerException(sourceRef, "Operands of ?: must have values");
                }

                if ((whenTrue.Type.IsStruct && whenTrue.Type.PointerDepth == 0) || (whenFalse.Type.IsStruct && whenFalse.Type.PointerDepth == 0)
                    || whenTrue.Type == TypeDesc.Void || whenFalse.Type == TypeDesc.Void)
                {
                    throw new CompilerException(sourceRef, "Operands of ?: must be scalars");
                }

                // the type of the result: the pointer, else the float, else the type of the first when both are alike
                var resultType = whenTrue.Type == whenFalse.Type ? whenTrue.Type
                    : whenTrue.Type.PointerDepth != 0 ? whenTrue.Type
                    : whenFalse.Type.PointerDepth != 0 ? whenFalse.Type
                    : (whenTrue.Type == TypeDesc.Float32 || whenFalse.Type == TypeDesc.Float32) ? TypeDesc.Float32
                    : TypeDesc.Int32;

                var trueConverter = CHelper.GetConverter(whenTrue.Type, resultType)
                    ?? throw new CompilerException(sourceRef, $"Cannot convert from {whenTrue.Type.Display} to {resultType.Display}");
                var falseConverter = CHelper.GetConverter(whenFalse.Type, resultType)
                    ?? throw new CompilerException(sourceRef, $"Cannot convert from {whenFalse.Type.Display} to {resultType.Display}");

                var labels = NewLabels(sourceRef);
                var elseLabel = labels("else");
                var endLabel = labels("endsel");

                return new VarRef(
                    Type: resultType,
                    Getter: CHelper.JoinLines(
                        condition.Getter,
                        $"beqz {elseLabel}",
                        whenTrue.Getter,
                        trueConverter,
                        $"b {endLabel}",
                        $"{elseLabel}:",
                        whenFalse.Getter,
                        falseConverter,
                        $"{endLabel}:"
                    )
                );
            }

            throw new CompilerException(GetSourceRef(conditionalExpression), "Unsupported conditional expression");
        }

        private Func<string, string> NewLabels(SourceRef sourceRef)
        {
            return CHelper.NextUniqueLabelGenerator?.Invoke()
                ?? throw new CompilerException(sourceRef, "Logical operators and ?: are not available outside of functions");
        }

        private static VarRef DecayArray(VarRef value) => value.Type.IsArray ? value with { Type = value.Type.Decay() } : value;

        private VarRef VisitLogicalOrExpression(LogicalOrExpressionContext logicalOrExpression)
        {
            if (logicalOrExpression.logicalAndExpression() is { Length: > 0 } operands)
            {
                var result = VisitLogicalAndExpression(operands[0]);
                for (int x = 1; x < operands.Length; x++)
                {
                    result = ShortCircuit(isAnd: false, result, VisitLogicalAndExpression(operands[x]), GetSourceRef(operands[x]));
                }
                return result;
            }

            throw new CompilerException(GetSourceRef(logicalOrExpression), "Unsupported logical or expression");
        }

        private VarRef VisitLogicalAndExpression(LogicalAndExpressionContext logicalAndExpression)
        {
            if (logicalAndExpression.inclusiveOrExpression() is { Length: > 0 } operands)
            {
                var result = VisitInclusiveOrExpression(operands[0]);
                for (int x = 1; x < operands.Length; x++)
                {
                    result = ShortCircuit(isAnd: true, result, VisitInclusiveOrExpression(operands[x]), GetSourceRef(operands[x]));
                }
                return result;
            }

            throw new CompilerException(GetSourceRef(logicalAndExpression), "Unsupported logical and expression");
        }

        /// <summary>
        /// `a && b` and `a || b`, as real data does it: the left side is evaluated, and when it decides the result
        /// the right side is skipped.
        ///   a; dup; beqz Lend; b; land; Lend:        (a || b: bnez, lor)
        /// The value is 0 or 1.
        /// </summary>
        private VarRef ShortCircuit(bool isAnd, VarRef left, VarRef right, SourceRef sourceRef)
        {
            left = Promote(DecayArray(left));
            right = Promote(DecayArray(right));

            foreach (var operand in new[] { left, right })
            {
                if (operand.Getter == null || operand.Type == TypeDesc.Void || (operand.Type.IsStruct && operand.Type.PointerDepth == 0))
                {
                    throw new CompilerException(sourceRef, "Operands of a logical operator must be scalars");
                }
            }

            // When the left side decides, it is the result, so for || it must be 0 or 1 (for &&, it is the 0 itself).
            // The result of a comparison is already 0 or 1.
            var leftBool = isAnd || IsZeroOrOne(left.Getter!)
                ? left.Getter
                : CHelper.JoinLines(left.Getter, left.Type.Comparators?.Snez ?? throw new CompilerException(sourceRef, $"Cannot test {left.Type.Display}"));

            var end = NewLabels(sourceRef)("endlogic");

            return new VarRef(
                Type: TypeDesc.Int32,
                Getter: CHelper.JoinLines(
                    leftBool,
                    "dup",
                    $"{(isAnd ? "beqz" : "bnez")} {end}",
                    right.Getter,
                    isAnd ? "land" : "lor",
                    $"{end}:"
                )
            );
        }

        private static bool IsZeroOrOne(string code)
        {
            var last = code.Split('\n').Select(it => it.Trim()).LastOrDefault(it => it != "");
            return last is "seqz" or "snez" or "sltz" or "slez" or "sgez" or "sgtz"
                or "seqz.s" or "snez.s" or "sltz.s" or "slez.s" or "sgez.s" or "sgtz.s" or "land" or "lor";
        }

        private VarRef VisitInclusiveOrExpression(InclusiveOrExpressionContext inclusiveOrExpression)
        {
            if (inclusiveOrExpression.exclusiveOrExpression() is var exclusiveOrExpressions)
            {
                switch (exclusiveOrExpressions?.Length)
                {
                    case 1:
                        return VisitExclusiveOrExpression(exclusiveOrExpressions[0]);
                    case >= 2:
                        // 1 ^ 2
                        var lvalTree = exclusiveOrExpressions[0];
                        var lval = Promote(VisitExclusiveOrExpression(lvalTree));
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < exclusiveOrExpressions.Length - 1; x++)
                        {
                            var rvalTree = exclusiveOrExpressions[1 + x];
                            var rval = Promote(VisitExclusiveOrExpression(rvalTree));
                            if (rval.Getter == null)
                            {
                                throw new CompilerException(GetSourceRef(rvalTree), "Right value must have a getter");
                            }

                            var opTree = (ITerminalNode)inclusiveOrExpression.GetChild(1 + 2 * x);
                            var opType = opTree.Symbol.Type;

                            var converter = CHelper.GetConverter(FromType: rval.Type, ToType: TypeDesc.Int32)
                                ?? throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Cannot convert from {rval.Type.Display} to {TypeDesc.Int32.Display}"
                                );

                            VarRef Apply(string opName, string? lines)
                            {
                                if (lines == null)
                                {
                                    throw new CompilerException(
                                        CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                        $"Operation {opName} not supported for type {lval.Type.Display}"
                                    );
                                }

                                return CHelper.CombineGetterWithInstruction(
                                    lval: lval,
                                    lines: CHelper.JoinLines(
                                        rval.Getter,
                                        converter,
                                        lines
                                    )
                                );
                            }

                            if (false)
                            { }
                            else if (opType == CPP14Parser.Or)
                            {
                                lval = Apply("or", lval.Type.Math?.ApplyOr);
                            }
                            else
                            {
                                throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Unsupported inclusive or operator {opTree.GetText()}"
                                );
                            }
                        }

                        return lval;
                }
            }

            throw new CompilerException(GetSourceRef(inclusiveOrExpression), "Unsupported inclusive or expression");
        }

        private VarRef VisitExclusiveOrExpression(ExclusiveOrExpressionContext exclusiveOrExpression)
        {
            if (exclusiveOrExpression.andExpression() is var andExpressions)
            {
                switch (andExpressions?.Length)
                {
                    case 1:
                        return VisitAndExpression(andExpressions[0]);
                    case >= 2:
                        // 1 | 2
                        var lvalTree = andExpressions[0];
                        var lval = Promote(VisitAndExpression(lvalTree));
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < andExpressions.Length - 1; x++)
                        {
                            var rvalTree = andExpressions[1 + x];
                            var rval = Promote(VisitAndExpression(rvalTree));
                            if (rval.Getter == null)
                            {
                                throw new CompilerException(GetSourceRef(rvalTree), "Right value must have a getter");
                            }

                            var opTree = (ITerminalNode)exclusiveOrExpression.GetChild(1 + 2 * x);
                            var opType = opTree.Symbol.Type;

                            var converter = CHelper.GetConverter(FromType: rval.Type, ToType: TypeDesc.Int32)
                                ?? throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Cannot convert from {rval.Type.Display} to {TypeDesc.Int32.Display}"
                                );

                            VarRef Apply(string opName, string? lines)
                            {
                                if (lines == null)
                                {
                                    throw new CompilerException(
                                        CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                        $"Operation {opName} not supported for type {lval.Type.Display}"
                                    );
                                }

                                return CHelper.CombineGetterWithInstruction(
                                    lval: lval,
                                    lines: CHelper.JoinLines(
                                        rval.Getter,
                                        converter,
                                        lines
                                    )
                                );
                            }

                            if (false)
                            { }
                            else if (opType == CPP14Parser.Caret)
                            {
                                lval = Apply("xor", lval.Type.Math?.ApplyXor);
                            }
                            else
                            {
                                throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Unsupported exclusive or operator {opTree.GetText()}"
                                );
                            }
                        }

                        return lval;
                }
            }

            throw new CompilerException(GetSourceRef(exclusiveOrExpression), "Unsupported exclusive or expression");
        }

        private VarRef VisitAndExpression(AndExpressionContext andExpression)
        {
            if (andExpression.equalityExpression() is var equalityExpressions)
            {
                switch (equalityExpressions?.Length)
                {
                    case 1:
                        return VisitEqualityExpression(equalityExpressions[0]);
                    case >= 2:
                        // 1 & 2
                        var lvalTree = equalityExpressions[0];
                        var lval = Promote(VisitEqualityExpression(lvalTree));
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < equalityExpressions.Length - 1; x++)
                        {
                            var rvalTree = equalityExpressions[1 + x];
                            var rval = Promote(VisitEqualityExpression(rvalTree));
                            if (rval.Getter == null)
                            {
                                throw new CompilerException(GetSourceRef(rvalTree), "Right value must have a getter");
                            }

                            var opTree = (ITerminalNode)andExpression.GetChild(1 + 2 * x);
                            var opType = opTree.Symbol.Type;

                            var converter = CHelper.GetConverter(FromType: rval.Type, ToType: TypeDesc.Int32)
                                ?? throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Cannot convert from {rval.Type.Display} to {TypeDesc.Int32.Display}"
                                );

                            VarRef Apply(string opName, string? lines)
                            {
                                if (lines == null)
                                {
                                    throw new CompilerException(
                                        CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                        $"Operation {opName} not supported for type {lval.Type.Display}"
                                    );
                                }

                                return CHelper.CombineGetterWithInstruction(
                                    lval: lval,
                                    lines: CHelper.JoinLines(
                                        rval.Getter,
                                        converter,
                                        lines
                                    )
                                );
                            }

                            if (false)
                            { }
                            else if (opType == CPP14Parser.And)
                            {
                                lval = Apply("and", lval.Type.Math?.ApplyAnd);
                            }
                            else
                            {
                                throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Unsupported and operator {opTree.GetText()}"
                                );
                            }
                        }

                        return lval;
                }
            }

            throw new CompilerException(GetSourceRef(andExpression), "Unsupported and expression");
        }

        private VarRef VisitEqualityExpression(EqualityExpressionContext equalityExpression)
        {
            if (equalityExpression.relationalExpression() is var relationalExpressions)
            {
                switch (relationalExpressions?.Length)
                {
                    case 1:
                        return VisitRelationalExpression(relationalExpressions[0]);
                    case 2:
                        var lval = Promote(VisitRelationalExpression(relationalExpressions[0]));
                        var rval = Promote(VisitRelationalExpression(relationalExpressions[1]));

                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(relationalExpressions[0]), "Left value must have a getter");
                        }

                        if (rval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(relationalExpressions[1]), "Right value must have a getter");
                        }

                        var converter = CHelper.GetConverter(FromType: rval.Type, ToType: lval.Type)
                            ?? throw new CompilerException(GetSourceRef(relationalExpressions[1]), $"Cannot convert from {rval.Type.Display} to {lval.Type.Display}");

                        VarRef ApplySubWith(string opName, string? sub, string? lines)
                        {
                            if (lines == null)
                            {
                                throw new CompilerException(GetSourceRef(equalityExpression), $"Operation {opName} not supported for type {lval.Type.Display}");
                            }

                            return CHelper.CombineGetterWithInstruction(
                                lval: lval,
                                lines: CHelper.JoinLines(
                                    rval.Getter,
                                    converter,
                                    sub,
                                    lines
                                )
                            );
                        }

                        if (false)
                        { }
                        else if (equalityExpression.Equal().Any())
                        {
                            return ApplySubWith("equal", lval.Type.Math?.ApplySub, lval.Type.Comparators?.Seqz);
                        }
                        else if (equalityExpression.NotEqual().Any())
                        {
                            return ApplySubWith("notEqual", lval.Type.Math?.ApplySub, lval.Type.Comparators?.Snez);
                        }

                        break;
                }
            }

            throw new CompilerException(GetSourceRef(equalityExpression), "Unsupported equality expression");
        }

        /// <summary>Integer promotion: char and short take part in arithmetic as int, so the other operand is not narrowed to them.</summary>
        private static VarRef Promote(VarRef value) =>
            value.Type is { PointerDepth: 0, IsStruct: false, ElementType: null, Size: 1 or 2 } && value.Type.Math == LLMath.IntVer
                ? value with { Type = TypeDesc.Int32 }
                : value;

        private VarRef VisitRelationalExpression(RelationalExpressionContext relationalExpression)
        {
            if (relationalExpression.shiftExpression() is var shiftExpressions)
            {
                switch (shiftExpressions?.Length)
                {
                    case 1:
                        return VisitShiftExpression(shiftExpressions[0]);
                    case 2:
                        var lval = Promote(VisitShiftExpression(shiftExpressions[0]));
                        var rval = Promote(VisitShiftExpression(shiftExpressions[1]));

                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(relationalExpression), "Left value must have a getter");
                        }

                        if (rval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(relationalExpression), "Right value must have a getter");
                        }

                        var converter = CHelper.GetConverter(FromType: rval.Type, ToType: lval.Type)
                            ?? throw new CompilerException(GetSourceRef(relationalExpression), $"Cannot convert from {rval.Type.Display} to {lval.Type.Display}");

                        VarRef Apply(string opName, string? lines)
                        {
                            if (lines == null)
                            {
                                throw new CompilerException(GetSourceRef(relationalExpression), $"Operation {opName} not supported for type {lval.Type.Display}");
                            }

                            return CHelper.CombineGetterWithInstruction(
                                lval: lval,
                                lines: CHelper.JoinLines(
                                    rval.Getter,
                                    converter,
                                    // a < b  <=>  (a - b) < 0, then test the sign of the difference
                                    lval.Type.Math?.ApplySub,
                                    lines
                                )
                            );
                        }

                        if (false)
                        { }
                        else if (relationalExpression.LessEqual().Any())
                        {
                            return Apply("slez", lval.Type.Comparators?.Slez);
                        }
                        else if (relationalExpression.Less().Any())
                        {
                            return Apply("sltz", lval.Type.Comparators?.Sltz);
                        }
                        else if (relationalExpression.GreaterEqual().Any())
                        {
                            return Apply("sgez", lval.Type.Comparators?.Sgez);
                        }
                        else if (relationalExpression.Greater().Any())
                        {
                            return Apply("sgtz", lval.Type.Comparators?.Sgtz);
                        }

                        break;
                }
            }

            throw new CompilerException(GetSourceRef(relationalExpression), "Unsupported relational expression");
        }

        private VarRef VisitShiftExpression(ShiftExpressionContext shiftExpression)
        {
            if (shiftExpression.additiveExpression() is var additiveExpressions)
            {
                switch (additiveExpressions?.Length)
                {
                    case 1:
                        return VisitAdditiveExpression(additiveExpressions[0]);
                    case >= 2:
                        // 1 << 2
                        // 1 << 2 >> 3
                        var lvalTree = additiveExpressions[0];
                        var lval = Promote(VisitAdditiveExpression(lvalTree));
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < additiveExpressions.Length - 1; x++)
                        {
                            var rvalTree = additiveExpressions[1 + x];
                            var rval = Promote(VisitAdditiveExpression(rvalTree));
                            if (rval.Getter == null)
                            {
                                throw new CompilerException(GetSourceRef(rvalTree), "Right value must have a getter");
                            }

                            var opTree = (ITerminalNode)((ShiftOperatorContext)shiftExpression.GetChild(1 + 2 * x)).GetChild(0);
                            var opType = opTree.Symbol.Type;

                            var converter = CHelper.GetConverter(FromType: rval.Type, ToType: TypeDesc.Int32)
                                ?? throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Cannot convert from {rval.Type.Display} to {TypeDesc.Int32.Display}"
                                );

                            VarRef Apply(string opName, string? lines)
                            {
                                if (lines == null)
                                {
                                    throw new CompilerException(
                                        CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                        $"Operation {opName} not supported for type {lval.Type.Display}"
                                    );
                                }

                                return CHelper.CombineGetterWithInstruction(
                                    lval: lval,
                                    lines: CHelper.JoinLines(
                                        rval.Getter,
                                        converter,
                                        lines
                                    )
                                );
                            }

                            if (false)
                            { }
                            else if (opType == CPP14Parser.Less)
                            {
                                lval = Apply("sll", lval.Type.Math?.ApplySll);
                            }
                            else if (opType == CPP14Parser.Greater)
                            {
                                lval = Apply("sra", lval.Type.Math?.ApplySra);
                            }
                            else
                            {
                                throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Unsupported shift operator {opTree.GetText()}"
                                );
                            }
                        }

                        return lval;
                }
            }

            throw new CompilerException(GetSourceRef(shiftExpression), "Unsupported shift expression");
        }

        private VarRef VisitAdditiveExpression(AdditiveExpressionContext additiveExpression)
        {
            if (additiveExpression.multiplicativeExpression() is var multiplicativeExpressions)
            {
                switch (multiplicativeExpressions?.Length)
                {
                    case 1:
                        return VisitMultiplicativeExpression(multiplicativeExpressions[0]);
                    case >= 2:
                        // 1 + 2
                        // 1 + 2 - 3
                        var lvalTree = multiplicativeExpressions[0];
                        var lval = Promote(VisitMultiplicativeExpression(lvalTree));
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < multiplicativeExpressions.Length - 1; x++)
                        {
                            var rvalTree = multiplicativeExpressions[1 + x];
                            var rval = Promote(VisitMultiplicativeExpression(rvalTree));
                            if (rval.Getter == null)
                            {
                                throw new CompilerException(GetSourceRef(rvalTree), "Right value must have a getter");
                            }

                            var opTree = (ITerminalNode)additiveExpression.GetChild(1 + 2 * x);
                            var opType = opTree.Symbol.Type;

                            var converter = CHelper.GetConverter(FromType: rval.Type, ToType: lval.Type)
                                ?? throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Cannot convert from {rval.Type.Display} to {lval.Type.Display}"
                                );

                            VarRef Apply(string opName, string? lines)
                            {
                                if (lines == null)
                                {
                                    throw new CompilerException(
                                        CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                        $"Operation {opName} not supported for type {lval.Type.Display}"
                                    );
                                }

                                return CHelper.CombineGetterWithInstruction(
                                    lval: lval,
                                    lines: CHelper.JoinLines(
                                        rval.Getter,
                                        converter,
                                        lines
                                    )
                                );
                            }

                            if (false)
                            { }
                            else if (opType == CPP14Parser.Plus)
                            {
                                lval = Apply("add", lval.Type.Math?.ApplyAdd);
                            }
                            else if (opType == CPP14Parser.Minus)
                            {
                                lval = Apply("sub", lval.Type.Math?.ApplySub);
                            }
                            else if (opType == CPP14Parser.Star)
                            {
                                lval = Apply("mul", lval.Type.Math?.ApplyMul);
                            }
                            else if (opType == CPP14Parser.Div)
                            {
                                lval = Apply("div", lval.Type.Math?.ApplyDiv);
                            }
                            else if (opType == CPP14Parser.Mod)
                            {
                                lval = Apply("mod", lval.Type.Math?.ApplyMod);
                            }
                            else
                            {
                                throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Unsupported additive operator {opTree.GetText()}"
                                );
                            }
                        }

                        return lval;
                }
            }

            throw new CompilerException(GetSourceRef(additiveExpression), "Unsupported additive expression");
        }

        private VarRef VisitMultiplicativeExpression(MultiplicativeExpressionContext multiplicativeExpression)
        {
            if (multiplicativeExpression.pointerMemberExpression() is var pointerMemberExpressions)
            {
                switch (pointerMemberExpressions?.Length)
                {
                    case 1:
                        return VisitPointerMemberExpression(pointerMemberExpressions[0]);
                    case >= 2:
                        // 1 * 2
                        // 1 * 2 / 3
                        var lvalTree = pointerMemberExpressions[0];
                        var lval = Promote(VisitPointerMemberExpression(lvalTree));
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < pointerMemberExpressions.Length - 1; x++)
                        {
                            var rvalTree = pointerMemberExpressions[1 + x];
                            var rval = Promote(VisitPointerMemberExpression(rvalTree));
                            if (rval.Getter == null)
                            {
                                throw new CompilerException(GetSourceRef(rvalTree), "Right value must have a getter");
                            }

                            var opTree = (ITerminalNode)multiplicativeExpression.GetChild(1 + 2 * x);
                            var opType = opTree.Symbol.Type;

                            var converter = CHelper.GetConverter(FromType: rval.Type, ToType: lval.Type)
                                ?? throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Cannot convert from {rval.Type.Display} to {lval.Type.Display}"
                                );

                            VarRef Apply(string opName, string? lines)
                            {
                                if (lines == null)
                                {
                                    throw new CompilerException(
                                        CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                        $"Operation {opName} not supported for type {lval.Type.Display}"
                                    );
                                }

                                return CHelper.CombineGetterWithInstruction(
                                    lval: lval,
                                    lines: CHelper.JoinLines(
                                        rval.Getter,
                                        converter,
                                        lines
                                    )
                                );
                            }

                            if (false)
                            { }
                            else if (opType == CPP14Parser.Star)
                            {
                                lval = Apply("mul", lval.Type.Math?.ApplyMul);
                            }
                            else if (opType == CPP14Parser.Div)
                            {
                                lval = Apply("div", lval.Type.Math?.ApplyDiv);
                            }
                            else if (opType == CPP14Parser.Mod)
                            {
                                lval = Apply("mod", lval.Type.Math?.ApplyMod);
                            }
                            else
                            {
                                throw new CompilerException(
                                    CHelper.GetSourceRefFromSymbol(opTree.Symbol),
                                    $"Unsupported multiplicative operator {opTree.GetText()}"
                                );
                            }
                        }

                        return lval;
                }
            }

            throw new CompilerException(GetSourceRef(multiplicativeExpression), "Unsupported multiplicative expression");
        }

        private VarRef VisitPointerMemberExpression(PointerMemberExpressionContext pointerMemberExpression)
        {
            if (pointerMemberExpression.castExpression() is var castExpressions)
            {
                switch (castExpressions?.Length)
                {
                    case 1:
                        return VisitCastExpression(castExpressions[0]);
                    case 2:
                        var isMinus = new CastExpressionToMinusOrNotVisitor(GetSourceRef, CHelper)
                            .Visit(castExpressions[0]);
                        var rval = VisitCastExpression(castExpressions[1]);

                        if (isMinus)
                        {
                            return CHelper.CombineGetterWithInstruction(
                                rval,
                                rval.Type.Math?.ApplyNeg ?? throw new CompilerException(GetSourceRef(castExpressions[1]), $"Negation not supported for type {rval.Type.Display}")
                            );
                        }
                        else
                        {
                            return rval;
                        }
                }
            }
            throw new CompilerException(GetSourceRef(pointerMemberExpression), "Unsupported pointer member expression");
        }

        private VarRef VisitCastExpression(CastExpressionContext castExpression)
        {
            if (castExpression.unaryExpression() is var unaryExpression
                && unaryExpression != null)
            {
                return VisitUnaryExpression(unaryExpression);
            }

            if (castExpression.theTypeId() is var theTypeId
                && theTypeId != null
                && castExpression.castExpression() is var innerCastExpression
                && innerCastExpression != null)
            {
                var rval = VisitCastExpression(innerCastExpression);
                var targetType = VisitTheTypeId(theTypeId);

                var converter = CHelper.GetConverter(FromType: rval.Type, ToType: targetType)
                    ?? throw new CompilerException(GetSourceRef(theTypeId), $"Cannot convert from {rval.Type.Display} to {targetType.Display}");

                // the value now has the target type (`(char *)p`, `(float)i`)
                return CHelper.CombineGetterWithInstruction(
                    lval: rval,
                    lines: converter
                ) with { Type = targetType };
            }

            throw new CompilerException(GetSourceRef(castExpression), "Unsupported cast expression");
        }

        private TypeDesc VisitTheTypeId(TheTypeIdContext theTypeId)
        {
            if (theTypeId.typeSpecifierSeq() is var typeSpecifierSeq
                && typeSpecifierSeq != null)
            {
                var type = VisitTypeSpecifierSeq(typeSpecifierSeq);

                // `(char *)p`: the stars of the abstract declarator
                if (theTypeId.abstractDeclarator() is { } abstractDeclarator)
                {
                    var pointerAbstract = abstractDeclarator.pointerAbstractDeclarator()
                        ?? throw new CompilerException(GetSourceRef(abstractDeclarator), "Unsupported type in cast");
                    if (pointerAbstract.noPointerAbstractDeclarator() != null
                        || pointerAbstract.pointerOperator().Any(it => it.Star() == null))
                    {
                        throw new CompilerException(GetSourceRef(abstractDeclarator), "Unsupported type in cast");
                    }
                    foreach (var _ in pointerAbstract.pointerOperator())
                    {
                        type = type.MakePointerType();
                    }
                }

                return type;
            }

            throw new CompilerException(GetSourceRef(theTypeId), "Unsupported the type id");
        }

        private TypeDesc VisitTypeSpecifierSeq(TypeSpecifierSeqContext typeSpecifierSeq)
        {
            if (typeSpecifierSeq.typeSpecifier() is var typeSpecifiers
                && typeSpecifiers != null)
            {
                switch (typeSpecifiers.Length)
                {
                    case 1:
                        return ReduceTypeDesc1.ReduceTypeSpecifier1(typeSpecifiers[0]);
                    case 2:
                        return ReduceTypeDesc1.ReduceTypeSpecifier2(typeSpecifiers[0], typeSpecifiers[1]);
                }
            }

            throw new CompilerException(GetSourceRef(typeSpecifierSeq), "Unsupported type specifier seq");
        }

        /// <summary>
        /// `sizeof(type)`, `sizeof(expr)` and `sizeof expr`: a constant. The operand is not evaluated.
        /// </summary>
        private VarRef VisitSizeof(UnaryExpressionContext unaryExpression)
        {
            var sourceRef = GetSourceRef(unaryExpression);
            TypeDesc type;

            if (unaryExpression.theTypeId() is { } theTypeId)
            {
                // `sizeof(x)` also parses as a type id when x is a variable
                var text = theTypeId.Start.InputStream.GetText(new Antlr4.Runtime.Misc.Interval(theTypeId.Start.StartIndex, theTypeId.Stop.StopIndex));
                var isName = System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z_]\w*$")
                    && text is not ("int" or "char" or "short" or "long" or "float" or "double" or "void" or "signed" or "unsigned");
                type = isName && CHelper.GetVar(text) is { } variable
                    ? variable.Type
                    : VisitTheTypeId(theTypeId);
            }
            else if (unaryExpression.unaryExpression() is { } operand)
            {
                type = VisitUnaryExpression(operand).Type;
            }
            else
            {
                throw new CompilerException(sourceRef, "Unsupported sizeof");
            }

            return new VarRef(Type: TypeDesc.Int32, Getter: $"push {CHelper.SizeOfType(type, sourceRef)}");
        }

        private VarRef VisitUnaryExpression(UnaryExpressionContext unaryExpression)
        {
            if (unaryExpression.Sizeof() != null)
            {
                return VisitSizeof(unaryExpression);
            }

            if (unaryExpression.postfixExpression() is var postfixExpression
                && postfixExpression != null)
            {
                return VisitPostfixExpression(postfixExpression);
            }

            if (unaryExpression.unaryOperator() is var unaryOperator
                && unaryOperator != null
                && unaryExpression.unaryExpression() is var unaryExpressionSub
                && unaryExpressionSub != null
            )
            {
                var lval = VisitUnaryExpression(unaryExpressionSub);

                if (unaryOperator.And() != null)
                {
                    // &x needs the address only (a struct has no value getter)
                    return CHelper.MakePointer(lval, GetSourceRef(unaryOperator));
                }

                if (lval.Getter == null)
                {
                    throw new CompilerException(GetSourceRef(unaryExpressionSub), "Left value must have a getter");
                }

                if (false)
                { }
                else if (unaryOperator.Minus() != null)
                {
                    return CHelper.CombineGetterWithInstruction(
                        lval: lval,
                        lines: lval.Type.Math?.ApplyNeg ?? throw new CompilerException(GetSourceRef(unaryOperator), $"Negation not supported for type {lval.Type.Display}")
                    );
                }
                else if (unaryOperator.Plus() != null)
                {
                    return lval;
                }
                else if (unaryOperator.Not() != null)
                {
                    return CHelper.CombineGetterWithInstruction(
                        lval: lval,
                        lines: lval.Type.Comparators?.Seqz ?? throw new CompilerException(GetSourceRef(unaryOperator), $"Logical NOT not supported for type {lval.Type.Display}")
                    );
                }
                else if (unaryOperator.Star() != null)
                {
                    return CHelper.Deref(lval, GetSourceRef(unaryOperator));
                }
                else if (unaryOperator.And() != null)
                {
                    return CHelper.MakePointer(lval, GetSourceRef(unaryOperator));
                }
                else if (unaryOperator.Tilde() != null)
                {
                    return CHelper.CombineGetterWithInstruction(
                        lval: lval,
                        lines: lval.Type.Math?.ApplyNot ?? throw new CompilerException(GetSourceRef(unaryOperator), $"Bitwise NOT not supported for type {lval.Type.Display}")
                    );
                }
            }

            if (unaryExpression.unaryExpression() is var innerUnaryExpression
                && innerUnaryExpression != null
            )
            {
                var lval = VisitUnaryExpression(innerUnaryExpression);

                if (lval.Getter == null)
                {
                    throw new CompilerException(GetSourceRef(innerUnaryExpression), "Left value must have a getter");
                }

                if (lval.Setter == null)
                {
                    throw new CompilerException(GetSourceRef(innerUnaryExpression), "Left value must have a setter");
                }

                var converter = CHelper.GetConverter(FromType: TypeDesc.Int32, ToType: lval.Type)
                    ?? throw new CompilerException(GetSourceRef(innerUnaryExpression), $"Cannot convert from {TypeDesc.Int32.Display} to {lval.Type.Display}");

                VarRef Apply(string opName, string? lines)
                {
                    if (lines == null)
                    {
                        throw new CompilerException(
                            GetSourceRef(innerUnaryExpression),
                            $"Operation {opName} not supported for type {lval.Type.Display}"
                        );
                    }

                    var add = lval.Type.Math?.ApplyAdd ?? throw new CompilerException(GetSourceRef(innerUnaryExpression), $"Addition not supported for type {lval.Type.Display}");

                    // ++a, --a: the value is the new one
                    return new VarRef(
                        Type: lval.Type,
                        Getter: lval.Target is { } target
                            ? CHelper.JoinLines(target.Address, "dup", target.Load, lines, converter, add, CHelper.StoreTail(target))
                            : CHelper.JoinLines(lval.Getter, lines, converter, add, "dup", lval.Setter)
                    );
                }

                if (false)
                { }
                else if (unaryExpression.PlusPlus() != null)
                {
                    return Apply("PlusPlus", "push 1");
                }
                else if (unaryExpression.MinusMinus() != null)
                {
                    return Apply("MinusMinus", "push -1");
                }
                else
                {
                    throw new CompilerException(GetSourceRef(unaryExpression), "Unsupported unary operator");
                }
            }

            throw new CompilerException(GetSourceRef(unaryExpression), "Unsupported unary expression");
        }

        private VarRef VisitPostfixExpression(PostfixExpressionContext postfixExpression)
        {
            if (postfixExpression.primaryExpression() is var primaryExpression
                && primaryExpression != null)
            {
                return VisitPrimaryExpression(primaryExpression);
            }

            if (postfixExpression.postfixExpression() is { } arrayExpression
                && postfixExpression.LeftBracket() != null)
            {
                // a[i]
                var indexExpression = postfixExpression.expression()
                    ?? throw new CompilerException(GetSourceRef(postfixExpression), "Braced initializer is not supported as an array index");
                var array = VisitPostfixExpression(arrayExpression);
                var index = VisitExpression(indexExpression);
                return CHelper.Index(array, index, GetSourceRef(postfixExpression));
            }

            if (postfixExpression.postfixExpression() is var callee
                && callee != null
                && postfixExpression.LeftParen() != null)
            {
                return VisitCall(postfixExpression, callee);
            }

            if (postfixExpression.postfixExpression() is var objectExpression
                && objectExpression != null
                && (postfixExpression.Dot() != null || postfixExpression.Arrow() != null))
            {
                // s.member, p->member
                var memberName = postfixExpression.idExpression()?.unqualifiedId()?.Identifier()?.GetText()
                    ?? throw new CompilerException(GetSourceRef(postfixExpression), "Member name is expected");

                var obj = VisitPostfixExpression(objectExpression);

                if (postfixExpression.Arrow() != null)
                {
                    if (obj.Type.PointerDepth != 1 || obj.Type.BaseType?.IsStruct != true)
                    {
                        throw new CompilerException(GetSourceRef(postfixExpression), $"Left of -> must be a pointer to struct, but {obj.Type.Display}");
                    }
                    obj = CHelper.Deref(obj, GetSourceRef(postfixExpression));
                }

                if (obj.Members == null)
                {
                    throw new CompilerException(GetSourceRef(postfixExpression), $"Left of . must be a struct, but {obj.Type.Display}");
                }

                var member = obj.Members.TryGetValue(memberName, out var found)
                    ? found
                    : throw new CompilerException(GetSourceRef(postfixExpression), $"{obj.Type.Display} has no member '{memberName}'");

                if (obj.Prelude != null)
                {
                    // member of a returned struct, e.g. f().x: run the call first. It is an rvalue.
                    return member.Members != null
                        ? member with { Prelude = obj.Prelude }
                        : new VarRef(
                            Type: member.Type,
                            Getter: CHelper.JoinLines(obj.Prelude, member.Getter),
                            GetPointer: null
                        );
                }

                return member;
            }

            if (postfixExpression.postfixExpression() is var innerPostfixExpression
                && innerPostfixExpression != null)
            {
                var lval = VisitPostfixExpression(innerPostfixExpression);

                if (lval.Getter == null)
                {
                    throw new CompilerException(GetSourceRef(innerPostfixExpression), "Left value must have a getter");
                }

                if (lval.Setter == null)
                {
                    throw new CompilerException(GetSourceRef(innerPostfixExpression), "Left value must have a setter");
                }

                var converter = CHelper.GetConverter(FromType: TypeDesc.Int32, ToType: lval.Type)
                    ?? throw new CompilerException(GetSourceRef(innerPostfixExpression), $"Cannot convert from {TypeDesc.Int32.Display} to {lval.Type.Display}");

                VarRef Apply(string opName, string? lines)
                {
                    if (lines == null)
                    {
                        throw new CompilerException(
                            GetSourceRef(innerPostfixExpression),
                            $"Operation {opName} not supported for type {lval.Type.Display}"
                        );
                    }

                    var add = lval.Type.Math?.ApplyAdd ?? throw new CompilerException(GetSourceRef(innerPostfixExpression), $"Addition not supported for type {lval.Type.Display}");

                    // a++, a--: the value is the old one
                    if (lval.Target is { Size: 4 } target)
                    {
                        // address; old value; keep a copy of it in the slot; store the new one; read the copy back
                        return new VarRef(
                            Type: lval.Type,
                            Getter: CHelper.JoinLines(
                                target.Address,
                                "dup",
                                target.Load,
                                "dup",
                                $"pop.sp {CHelper.StoreValueSlotPlaceholder}",
                                lines,
                                converter,
                                add,
                                "memcpy 0",
                                $"push.d.sp {CHelper.StoreValueSlotPlaceholder}"
                            )
                        );
                    }

                    return new VarRef(
                        Type: lval.Type,
                        Getter: CHelper.JoinLines(lval.Getter, "dup", lines, converter, add, lval.Setter)
                    );
                }

                // a++, a--

                if (false)
                { }
                else if (postfixExpression.PlusPlus() != null)
                {
                    return Apply("PlusPlus", "push 1");
                }
                else if (postfixExpression.MinusMinus() != null)
                {
                    return Apply("MinusMinus", "push -1");
                }
                else
                {
                    throw new CompilerException(GetSourceRef(postfixExpression), "Unsupported postfix operator");
                }
            }

            throw new CompilerException(GetSourceRef(postfixExpression), "Unsupported postfix expression");
        }

        /// <summary>
        /// func(a, b, c)
        ///
        /// Arguments are pushed in reverse order, so that the first argument is on top of tp.
        /// The callee pops them in declaration order (pop.sp 0, pop.sp 4, ...).
        /// </summary>
        private VarRef VisitCall(PostfixExpressionContext postfixExpression, PostfixExpressionContext callee)
        {
            var funcName = callee.primaryExpression()?.idExpression()?.unqualifiedId()?.Identifier()?.GetText()
                ?? throw new CompilerException(GetSourceRef(callee), "Only direct function calls by name are supported");

            var sig = CHelper.GetFunc?.Invoke(funcName)
                ?? throw new CompilerException(GetSourceRef(callee), $"Unknown function '{funcName}'");

            var args = postfixExpression.expressionList()?.initializerList()?.initializerClause()
                ?? Array.Empty<InitializerClauseContext>();

            if (args.Length != sig.NumIns)
            {
                throw new CompilerException(
                    GetSourceRef(postfixExpression),
                    $"Function '{funcName}' expects {sig.NumIns} argument(s) but {args.Length} given"
                );
            }

            var lines = new List<string?>();

            // Verified with bdEmu (MobileAiExec): syscall args are pushed first-to-last, so the
            // first argument is the deepest tp slot (trap_puti pushes name, then value).
            // jal args are pushed last-to-first because the callee pops them into sp slots 0, 4, ...
            var isSyscall = sig.Syscall != null;
            for (int i = 0; i < args.Length; i++)
            {
                var x = isSyscall ? i : args.Length - 1 - i;
                var assignmentExpression = args[x].assignmentExpression()
                    ?? throw new CompilerException(GetSourceRef(args[x]), "Braced initializer is not supported as an argument");
                var arg = VisitAssignment(assignmentExpression);

                var paramType = sig.ParamTypes?[x];
                var argIsStruct = arg.Type.IsStruct && arg.Type.PointerDepth == 0;
                var paramIsStruct = paramType != null && paramType.IsStruct && paramType.PointerDepth == 0;
                if (argIsStruct || paramIsStruct)
                {
                    // struct by value: the address of the struct is pushed like any other argument, and the callee
                    // copies the struct into its own frame with one `memcpy.sp` (see the prologue). The struct is copied
                    // before the jal's frame is built, so the callee has its own copy.
                    if (!argIsStruct || !paramIsStruct || arg.Type.BaseName != paramType!.BaseName)
                    {
                        throw new CompilerException(
                            GetSourceRef(args[x]),
                            $"Cannot convert argument {x + 1} of '{funcName}' from {arg.Type.Display} to {paramType?.Display ?? "a syscall parameter"}"
                        );
                    }
                    lines.Add(arg.Prelude); // the argument is the result of a call returning a struct
                    lines.Add(arg.GetPointer ?? throw new CompilerException(GetSourceRef(args[x]), "Argument struct has no address"));
                    continue;
                }

                if (arg.Getter == null)
                {
                    throw new CompilerException(GetSourceRef(args[x]), "Argument must have a getter");
                }
                lines.Add(arg.Getter);

                if (sig.ParamTypes != null)
                {
                    var converter = CHelper.GetConverter(FromType: arg.Type, ToType: sig.ParamTypes[x])
                        ?? throw new CompilerException(
                            GetSourceRef(args[x]),
                            $"Cannot convert argument {x + 1} of '{funcName}' from {arg.Type.Display} to {sig.ParamTypes[x].Display}"
                        );
                    lines.Add(converter);
                }
            }

            if (sig.Syscall is var (table, index))
            {
                lines.Add($"syscall {table}, {index} ; {funcName}");
            }
            else
            {
                lines.Add($"jal {CHelper.NextJalFrameSize()}, {funcName}");
            }

            if (sig.RetType.IsStruct && sig.RetType.PointerDepth == 0)
            {
                // A struct is returned as one pointer on tp, pointing into the dead frame of the callee.
                // It stays valid only until the next jal, so it is copied right after the call.
                var call = CHelper.JoinLines(lines.ToArray())!;
                var slots = StructVar.SlotsOf(sig.RetType);
                var tempOffset = 4 * CHelper.AllocateTemp(slots);
                var result = StructVar.Build(
                    sig.RetType,
                    tempOffset,
                    $"{funcName}()",
                    getter: (off, path) => $"push.d.sp {off} ; {path}",
                    setter: null,
                    pointer: (off, path) => $"push.sp {off} ; &{path}"
                );
                return result with
                {
                    Prelude = CHelper.JoinLines(call, $"memcpy.sp {4 * slots}, {tempOffset} ; {funcName}()"),
                    DiscardCode = call,
                };
            }

            return new VarRef(
                Type: sig.RetType,
                Getter: CHelper.JoinLines(lines.ToArray())
            );
        }

        /// <summary>
        /// Decode "text" or 'c' (quotes included) into bytes. Supports \n \t \r \0 \a \b \f \v \\ \' \" \? \xHH and octal.
        /// </summary>
        internal static byte[] UnescapeStringLiteral(string text, SourceRef sourceRef)
        {
            var body = text.Substring(1, text.Length - 2);
            var result = new List<byte>();
            for (int x = 0; x < body.Length; x++)
            {
                var c = body[x];
                if (c != '\\')
                {
                    var chars = (char.IsHighSurrogate(c) && x + 1 < body.Length)
                        ? body.Substring(x++, 2)
                        : c.ToString();
                    result.AddRange(Encoding.UTF8.GetBytes(chars));
                    continue;
                }
                if (++x >= body.Length)
                {
                    throw new CompilerException(sourceRef, "Invalid escape sequence");
                }
                c = body[x];
                switch (c)
                {
                    case 'n': result.Add(10); break;
                    case 't': result.Add(9); break;
                    case 'r': result.Add(13); break;
                    case 'a': result.Add(7); break;
                    case 'b': result.Add(8); break;
                    case 'f': result.Add(12); break;
                    case 'v': result.Add(11); break;
                    case '\\': case '\'': case '"': case '?': result.Add((byte)c); break;
                    case 'x':
                        {
                            int v = 0, n = 0;
                            while (x + 1 < body.Length && Uri.IsHexDigit(body[x + 1]))
                            {
                                v = v * 16 + Convert.ToInt32(body[++x].ToString(), 16);
                                n++;
                            }
                            if (n == 0) throw new CompilerException(sourceRef, "Invalid \\x escape sequence");
                            result.Add((byte)v);
                            break;
                        }
                    case >= '0' and <= '7':
                        {
                            int v = c - '0';
                            for (int n = 1; n < 3 && x + 1 < body.Length && body[x + 1] is >= '0' and <= '7'; n++)
                            {
                                v = v * 8 + (body[++x] - '0');
                            }
                            result.Add((byte)v);
                            break;
                        }
                    default:
                        throw new CompilerException(sourceRef, $"Unsupported escape sequence \\{c}");
                }
            }
            return result.ToArray();
        }

        private VarRef VisitPrimaryExpression(PrimaryExpressionContext primaryExpression)
        {
            if (primaryExpression.literal() is var strLiterals
                && strLiterals != null
                && 1 <= strLiterals.Length
                && strLiterals.All(it => it.StringLiteral() != null))
            {
                // "abc" "def" are concatenated. Placed in bd, the value is its address (char *).
                var bytes = strLiterals
                    .SelectMany(it => UnescapeStringLiteral(it.StringLiteral().GetText(), GetSourceRef(it)))
                    .ToArray();
                var label = CHelper.AddStringLiteral?.Invoke(bytes)
                    ?? throw new CompilerException(GetSourceRef(primaryExpression), "String literal is not available here");
                return new VarRef(
                    Type: TypeDesc.Int8.MakePointerType(),
                    Getter: $"push.bd {label}"
                );
            }

            if (primaryExpression.literal() is var literalList
                && literalList != null
                && literalList.Length == 1)
            {
                if (literalList[0].CharacterLiteral() is var charLiteral && charLiteral != null)
                {
                    var charBytes = UnescapeStringLiteral(charLiteral.GetText(), GetSourceRef(literalList[0]));
                    if (charBytes.Length != 1)
                    {
                        throw new CompilerException(GetSourceRef(literalList[0]), "Character literal must be a single byte");
                    }
                    return new VarRef(Type: TypeDesc.Int32, Getter: $"push {charBytes[0]}");
                }

                var literal = literalList[0];
                if (false)
                { }
                else if (literal.IntegerLiteral() is { } integerLiteral)
                {
                    return new VarRef(
                        Type: TypeDesc.Int32,
                        Getter: $"push {integerLiteral.GetText()}"
                    );
                }
                else if (literal.FloatingLiteral() is { } floatingLiteral)
                {
                    return new VarRef(
                        Type: TypeDesc.Float32,
                        Getter: $"push.s {floatingLiteral.GetText()}"
                    );
                }
                else
                {
                    throw new CompilerException(GetSourceRef(literal), "Unsupported literal type");
                }
            }

            if (primaryExpression.idExpression() is var idExpression
                && idExpression != null)
            {
                return VisitIdExpression(idExpression);
            }

            if (primaryExpression.expression() is var expression
                && expression != null)
            {
                return VisitExpression(expression);
            }

            throw new CompilerException(GetSourceRef(primaryExpression), "Unsupported primary expression");
        }

        private VarRef VisitIdExpression(IdExpressionContext idExpression)
        {
            if (idExpression.unqualifiedId() is var unqualifiedId
                && unqualifiedId != null)
            {
                return VisitUnqualifiedId(unqualifiedId);
            }

            throw new CompilerException(GetSourceRef(idExpression), "Unsupported id expression");
        }

        private VarRef VisitUnqualifiedId(UnqualifiedIdContext unqualifiedId)
        {
            if (unqualifiedId.Identifier() is var identifier
                && identifier != null)
            {
                var varName = identifier.GetText();
                var var0 = CHelper.GetVar(varName);
                if (var0 is null)
                {
                    throw new CompilerException(GetSourceRef(unqualifiedId), $"Unknown variable '{varName}'");
                }
                return var0;
            }

            throw new CompilerException(GetSourceRef(unqualifiedId), "Unsupported unqualified id");
        }
    }
}
