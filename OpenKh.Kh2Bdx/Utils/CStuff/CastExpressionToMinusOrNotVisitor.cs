using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    internal record CastExpressionToMinusOrNotVisitor(
        Func<ParserRuleContext, SourceRef> GetSourceRef,
        CHelper CHelper)
    {
        internal bool Visit(CPP14Parser.CastExpressionContext castExpression)
        {
            if (castExpression.unaryExpression() is var unaryExpression
                && unaryExpression != null)
            {
                return VisitUnaryExpression(unaryExpression);
            }

            throw new CompilerException(GetSourceRef(castExpression), "Unsupported cast expression");
        }

        private bool VisitUnaryExpression(CPP14Parser.UnaryExpressionContext unaryExpression)
        {
            if (unaryExpression.unaryOperator() is var unaryOperator
                && unaryOperator != null)
            {
                if (unaryOperator.Minus() != null)
                {
                    return true;
                }
                else if (unaryOperator.Plus() != null)
                {
                    return false;
                }
                else
                {
                    throw new CompilerException(GetSourceRef(unaryOperator), "Invalid unary operator");
                }
            }
            else
            {
                throw new CompilerException(GetSourceRef(unaryExpression), "Unsupported unary expression");
            }
        }
    }
}
