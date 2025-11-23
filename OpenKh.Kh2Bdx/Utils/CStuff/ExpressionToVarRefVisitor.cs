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
                var lval = VisitLogicalOrExpression(logicalOrExpression);
                var rval = VisitAssignment(initializerClause.assignmentExpression());

                var assignmentOperator = assignmentExpression.assignmentOperator();

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

                if (assignmentOperator == null)
                {
                    // simple assignment
                    return CHelper.CombineGetterWithInstruction(
                        lval: lval,
                        lines: CHelper.JoinLines(
                            converter,
                            lval.Setter
                        )
                    );
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

                        return CHelper.CombineGetterWithInstruction(
                            lval: lval,
                            lines: CHelper.JoinLines(
                                rval.Getter,
                                converter,
                                lines,
                                lval.Setter
                            )
                        );
                    }

                    if (false)
                    { }
                    else if (assignmentOperator.Assign() != null)
                    {
                        return CHelper.CombineGetterWithInstruction(
                            lval: lval,
                            lines: CHelper.JoinLines(
                                rval.Getter,
                                converter,
                                lval.Setter
                            )
                        );
                    }
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
                return VisitLogicalOrExpression(logicalOrExpression);
            }

            throw new CompilerException(GetSourceRef(conditionalExpression), "Unsupported conditional expression");
        }

        private VarRef VisitLogicalOrExpression(LogicalOrExpressionContext logicalOrExpression)
        {
            if (logicalOrExpression.logicalAndExpression() is var logicalAndExpressions)
            {
                switch (logicalAndExpressions?.Length)
                {
                    case 1:
                        return VisitLogicalAndExpression(logicalAndExpressions[0]);
                }
            }

            throw new CompilerException(GetSourceRef(logicalOrExpression), "Unsupported logical or expression");
        }

        private VarRef VisitLogicalAndExpression(LogicalAndExpressionContext logicalAndExpression)
        {
            if (logicalAndExpression.inclusiveOrExpression() is var inclusiveOrExpressions)
            {
                switch (inclusiveOrExpressions?.Length)
                {
                    case 1:
                        return VisitInclusiveOrExpression(inclusiveOrExpressions[0]);
                }
            }

            throw new CompilerException(GetSourceRef(logicalAndExpression), "Unsupported logical and expression");
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
                        var lval = VisitExclusiveOrExpression(lvalTree);
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < exclusiveOrExpressions.Length - 1; x++)
                        {
                            var rvalTree = exclusiveOrExpressions[1 + x];
                            var rval = VisitExclusiveOrExpression(rvalTree);
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
                        var lval = VisitAndExpression(lvalTree);
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < andExpressions.Length - 1; x++)
                        {
                            var rvalTree = andExpressions[1 + x];
                            var rval = VisitAndExpression(rvalTree);
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
                        var lval = VisitEqualityExpression(lvalTree);
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < equalityExpressions.Length - 1; x++)
                        {
                            var rvalTree = equalityExpressions[1 + x];
                            var rval = VisitEqualityExpression(rvalTree);
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
                        var lval = VisitRelationalExpression(relationalExpressions[0]);
                        var rval = VisitRelationalExpression(relationalExpressions[1]);

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

        private VarRef VisitRelationalExpression(RelationalExpressionContext relationalExpression)
        {
            if (relationalExpression.shiftExpression() is var shiftExpressions)
            {
                switch (shiftExpressions?.Length)
                {
                    case 1:
                        return VisitShiftExpression(shiftExpressions[0]);
                    case 2:
                        var lval = VisitShiftExpression(shiftExpressions[0]);
                        var rval = VisitShiftExpression(shiftExpressions[1]);

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
                        var lval = VisitAdditiveExpression(lvalTree);
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < additiveExpressions.Length - 1; x++)
                        {
                            var rvalTree = additiveExpressions[1 + x];
                            var rval = VisitAdditiveExpression(rvalTree);
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
                        var lval = VisitMultiplicativeExpression(lvalTree);
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < multiplicativeExpressions.Length - 1; x++)
                        {
                            var rvalTree = multiplicativeExpressions[1 + x];
                            var rval = VisitMultiplicativeExpression(rvalTree);
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
                        var lval = VisitPointerMemberExpression(lvalTree);
                        if (lval.Getter == null)
                        {
                            throw new CompilerException(GetSourceRef(lvalTree), "Left value must have a getter");
                        }

                        for (int x = 0; x < pointerMemberExpressions.Length - 1; x++)
                        {
                            var rvalTree = pointerMemberExpressions[1 + x];
                            var rval = VisitPointerMemberExpression(rvalTree);
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

                return CHelper.CombineGetterWithInstruction(
                    lval: rval,
                    lines: converter
                );
            }

            throw new CompilerException(GetSourceRef(castExpression), "Unsupported cast expression");
        }

        private TypeDesc VisitTheTypeId(TheTypeIdContext theTypeId)
        {
            if (theTypeId.typeSpecifierSeq() is var typeSpecifierSeq
                && typeSpecifierSeq != null)
            {
                return VisitTypeSpecifierSeq(typeSpecifierSeq);
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

        private VarRef VisitUnaryExpression(UnaryExpressionContext unaryExpression)
        {
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

                    return CHelper.CombineGetterWithInstruction(
                        lval: lval,
                        lines: CHelper.JoinLines(
                            "dup",
                            lines,
                            converter,
                            lval.Type.Math?.ApplyAdd ?? throw new CompilerException(GetSourceRef(innerUnaryExpression), $"Addition not supported for type {lval.Type.Display}"),
                            lval.Setter
                        )
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

                    return CHelper.CombineGetterWithInstruction(
                        lval: lval,
                        lines: CHelper.JoinLines(
                            lines,
                            converter,
                            lval.Type.Math?.ApplyAdd ?? throw new CompilerException(GetSourceRef(innerPostfixExpression), $"Addition not supported for type {lval.Type.Display}"),
                            "dup",
                            lval.Setter
                        )
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

        private VarRef VisitPrimaryExpression(PrimaryExpressionContext primaryExpression)
        {
            if (primaryExpression.literal() is var literalList
                && literalList != null
                && literalList.Length == 1)
            {
                var literal = literalList[0];
                if (false)
                { }
                else if (literal.IntegerLiteral() is var integerLiteral)
                {
                    return new VarRef(
                        Type: TypeDesc.Int32,
                        Getter: $"push {integerLiteral.GetText()}"
                    );
                }
                else if (literal.FloatingLiteral() is var floatingLiteral)
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
