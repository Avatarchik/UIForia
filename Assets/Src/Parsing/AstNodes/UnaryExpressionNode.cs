using System;

namespace Src {

    public class UnaryExpressionNode : ExpressionNode {

        public readonly UnaryOperatorType op;
        public readonly ExpressionNode expression;

        public UnaryExpressionNode(ExpressionNode expression, UnaryOperatorType op) : base(ExpressionNodeType.Unary) {
            this.expression = expression;
            this.op = op;
        }

        public override Type GetYieldedType(ContextDefinition context) {
            switch (op) {
                case UnaryOperatorType.Not:
                    return typeof(bool);
                default:
                    return expression.GetYieldedType(context);
            }
        }

    }

}