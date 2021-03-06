using System;
using System.Linq;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Extensions.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Parsing;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors
{
    public class CypherTranslatingExpressionVisitor : ThrowingExpressionVisitor
    {
        private readonly IExpressionFragmentTranslator _compositeExpressionFragmentTranslator;
        
        private readonly CypherQueryModelVisitor _queryModelVisitor;

        private readonly IRelationalTypeMapper _relationalTypeMapper;

        private readonly ReadOnlyExpression _targetReadOnlyExpression;

        private readonly bool _inReturn;

        private bool _isTopLevelReturn;

        
        public CypherTranslatingExpressionVisitor(
            [NotNull] SqlTranslatingExpressionVisitorDependencies dependencies,
            [NotNull] CypherQueryModelVisitor queryModelVisitor,
            [CanBeNull] ReadOnlyExpression targetReadOnlyExpresion = null,
            [CanBeNull] Expression topLevelWhere = null,
            bool inReturn = false
        )
        {
            Check.NotNull(dependencies, nameof(dependencies));
            Check.NotNull(queryModelVisitor, nameof(queryModelVisitor));

            _compositeExpressionFragmentTranslator = dependencies.CompositeExpressionFragmentTranslator;
            _targetReadOnlyExpression = targetReadOnlyExpresion;
            _inReturn = inReturn;
            _isTopLevelReturn = inReturn;
            
            _queryModelVisitor = queryModelVisitor;
            _relationalTypeMapper = dependencies.RelationalTypeMapper;            
        }

        /// <summary>
        /// Delegates everything that is not fragments, converts, negates, and new with 
        /// a top level return marker
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public override Expression Visit(Expression expression)
        {
            var translated = _compositeExpressionFragmentTranslator.Translate(expression);
            if (translated != null && translated != expression)
            {
                return Visit(translated);
            }

            if (expression != null
                && (expression.NodeType == ExpressionType.Convert
                    || expression.NodeType == ExpressionType.Negate
                    || expression.NodeType == ExpressionType.New))
            {
                return base.Visit(expression);
            }

            var isTopLevelReturn = _isTopLevelReturn;
            _isTopLevelReturn = false;

            try
            {
                return base.Visit(expression);
            }
            finally
            {
                _isTopLevelReturn = isTopLevelReturn;
            }
        }

        /// <summary>
        /// Binary
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        protected override Expression VisitBinary(BinaryExpression expression)
        {
            Check.NotNull(expression, nameof(expression));

            switch (expression.NodeType) {
                case ExpressionType.Coalesce: 
                {
                    var left = Visit(expression.Left);
                    var right = Visit(expression.Right);

                    return !(left is null) 
                        && !(right is null)
                        && left.Type != typeof(Expression[])
                        && right.Type != typeof(Expression[])
                        ? expression.Update(
                            left,
                            expression.Conversion,
                            right
                        )
                        : null;
                }
                case ExpressionType.Equal:
                case ExpressionType.NotEqual: 
                {
                    var unfolded = UnfoldStructuralComparison(
                        expression.NodeType,
                        ProcessComparisonExpression(expression)
                    );

                    return unfolded;
                }
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                {
                    return ProcessComparisonExpression(expression);
                }

                case ExpressionType.Add:
                case ExpressionType.Subtract:
                case ExpressionType.Multiply:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.ExclusiveOr:
                case ExpressionType.And:
                case ExpressionType.Or:
                {
                    var left = Visit(expression.Left);
                    var right = Visit(expression.Right);

                    return !(left is null) && !(right is null)
                        ? Expression.MakeBinary(
                            expression.NodeType,
                            left,
                            right,
                            expression.IsLiftedToNull,
                            expression.Method
                        )
                        : null;
                }
            }

            return null;
        }

        /// <summary>
        /// Visit conditional
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        protected override Expression VisitConditional(ConditionalExpression expression)
        {
            Check.NotNull(expression, nameof(expression));

            if (expression.IsNullPropagationCandidate(out var testExpression, out var resultExpression))
                // TODO: Null Check removal)
            {
                return Visit(resultExpression);
            }

            var test = Visit(expression.Test);
            if (test?.IsSimple() == true) {
                test = Expression.Equal(
                    test, 
                    Expression.Constant(true, typeof(bool))
                );
            }

            var ifTrue = Visit(expression.IfTrue);
            var ifFalse = Visit(expression.IfFalse);

            if (!(test is null) && !(ifTrue is null) && !(ifFalse is null)) {
                // TODO: IfTrue or IfFalse is expression array

                // TODO: Invertion
                
                return expression.Update(test, ifTrue, ifFalse);
            }

            return null;
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            throw new NotImplementedException();
        }

        protected override Expression VisitMember(MemberExpression memberExpression)
        {
            Check.NotNull(memberExpression, nameof(memberExpression));

            if (!(memberExpression.Expression.RemoveConvert() is QuerySourceReferenceExpression)
                && !(memberExpression.Expression.RemoveConvert() is SubQueryExpression))
            {
            }

            return TryBindMemberOrMethodToReadOnlyExpression(
                    memberExpression, 
                    (expression, visitor, binder) => visitor.BindMemberExpression(expression, binder)
                )
                ?? TryBindQuerySourcePropertyExpression(memberExpression)
                ?? _queryModelVisitor.BindMemberToOuterQueryParameter(memberExpression);
        }

        protected override Expression VisitUnary(UnaryExpression expression)
        {
            throw new NotImplementedException();
        }

        protected override Expression VisitNew(NewExpression expression)
        {
            throw new NotImplementedException();
        }

        protected override Expression VisitSubQuery(SubQueryExpression expression)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Visit constant
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        protected override Expression VisitConstant(ConstantExpression expression)
        {
            Check.NotNull(expression, nameof(expression));

            if (expression.Value is null) {
                return expression;
            }

            var underlyingType = expression.Type.UnwrapNullableType().UnwrapEnumType();
            if (underlyingType == typeof(Enum)) {
                underlyingType = expression.Value.GetType();
            }

            return !(_relationalTypeMapper.FindMapping(underlyingType) is null)
                ? expression
                : null;
        }

        protected override Expression VisitParameter(ParameterExpression expression)
        {
            throw new NotImplementedException();
        }

        protected override Expression VisitExtension(Expression expression)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Visit query source reference
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        protected override Expression VisitQuerySourceReference(QuerySourceReferenceExpression expression)
        {
            Check.NotNull(expression, nameof(expression));

            if (!_inReturn) {
                if (expression.ReferencedQuerySource is JoinClause joinClause) {
                    var entityType = _queryModelVisitor
                        .QueryCompilationContext
                        .FindEntityType(joinClause)
                        ?? _queryModelVisitor
                            .QueryCompilationContext
                            .Model
                            .FindEntityType(joinClause.ItemType);

                    if (!(entityType is null)) {
                        return Visit(
                            expression.CreateEFPropertyExpression(
                                entityType.FindPrimaryKey().Properties[0]
                            )
                        );
                    }

                    return null;
                }
            }

            var kind = expression
                .ReferencedQuerySource
                .ItemType
                .UnwrapNullableType()
                .UnwrapEnumType();
            if (!(_relationalTypeMapper.FindMapping(kind) is null)) {
                var readOnlyExpression = _queryModelVisitor.TryGetQuery(
                    expression.ReferencedQuerySource
                );

                if (!(readOnlyExpression is null)) {
                    var nested = readOnlyExpression.ReturnItems.FirstOrDefault() as ReadOnlyExpression;

                    if (!(nested is null)) {
                        // TODO: Lift nested
                    }
                }
            }

            return null;
        }

        protected override Exception CreateUnhandledItemException<T>(T unhandledItem, string visitMethod)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourceExpression"></param>
        /// <param name="Func<TExpression"></param>
        /// <param name="binder"></param>
        /// <returns></returns>
        private Expression TryBindMemberOrMethodToReadOnlyExpression<TExpression>(
            TExpression sourceExpression,
            Func<TExpression, CypherQueryModelVisitor, Func<IProperty, IQuerySource, ReadOnlyExpression, Expression>, Expression> binder 
        ) {
            Expression BindPropertyToReadOnlyExpression(
                IProperty property, 
                IQuerySource querySource, 
                ReadOnlyExpression readOnlyExpression
            ) => readOnlyExpression.BindProperty(property, querySource);

            var boundExpression = binder(
                sourceExpression,
                _queryModelVisitor,
                (property, querySource, readOnlyExpression) => {
                    var boundPropertyExpression = BindPropertyToReadOnlyExpression(
                        property,
                        querySource,
                        readOnlyExpression
                    );

                    if (!(_targetReadOnlyExpression is null)
                        && readOnlyExpression != _targetReadOnlyExpression) {
                        readOnlyExpression.AddReturnItem(boundPropertyExpression);
                    }

                    return boundPropertyExpression;
                }
            );

            if (boundExpression != null)
            {
                return boundExpression;
            }

            // TODO: Outer

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="memberExpression"></param>
        /// <returns></returns>
        private Expression TryBindQuerySourcePropertyExpression(MemberExpression memberExpression) {
            if (memberExpression.Expression is QuerySourceReferenceExpression qsre)
            {
                var readOnlyExpression = _queryModelVisitor.TryGetQuery(qsre.ReferencedQuerySource);

                return readOnlyExpression?.GetReturnForMemberInfo(memberExpression.Member);
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        private Expression ProcessComparisonExpression(
            BinaryExpression expression
        ) {
            var left = Visit(expression.Left);
            if (left is null) {
                return null;
            }

            var right = Visit(expression.Right);
            if (right is null) {
                return null;
            }

            var nullExpr = TransformNullComparison(left, right, expression.NodeType);
            if (!(nullExpr is null)) {
                return nullExpr;
            }

            if (left.Type != right.Type 
                && left.Type.UnwrapNullableType() == right.Type.UnwrapNullableType()) {
                if (left.Type.IsNullableType()) {
                    right = Expression.Convert(right, left.Type);
                }
                else {
                    left = Expression.Convert(left, right.Type);
                }
            }

            return left.Type == right.Type
                ? Expression.MakeBinary(
                    expression.NodeType,
                    left,
                    right
                )
                : null;
        }

        /// <summary>
        /// When expression is equals or not equals operation
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="kind"></param>
        /// <returns></returns>
        private static Expression TransformNullComparison(
            Expression left,
            Expression right,
            ExpressionType kind
        ) {
            if (kind == ExpressionType.Equal || kind == ExpressionType.NotEqual) {
                var isLeftNullConst = left.IsNullConstantExpression();
                var isRightNullConst = right.IsNullConstantExpression();

                if (isLeftNullConst || isRightNullConst) {
                    var notNull = (isLeftNullConst ? right : left).RemoveConvert();

                    if (notNull is NullableExpression nullableExpression) {
                        notNull = nullableExpression.Operand.RemoveConvert();
                    }

                    return kind == ExpressionType.Equal
                        ? (Expression)new IsNullExpression(notNull)
                        : Expression.Not(new IsNullExpression(notNull));
                }
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        private static Expression UnfoldStructuralComparison(
            ExpressionType kind,
            Expression expression
        ) {
            BinaryExpression be = expression as BinaryExpression;

            var leftConstantExpression = be?.Left as ConstantExpression;
            var leftExpressions = leftConstantExpression?.Value as Expression[];
            var rightConstantExpression = be?.Right as ConstantExpression;
            var rightExpressions = rightConstantExpression?.Value as Expression[];

            if (leftExpressions != null
                && rightConstantExpression != null
                && rightConstantExpression.Value == null)
            {
                rightExpressions
                    = Enumerable
                        .Repeat<Expression>(
                            rightConstantExpression, 
                            leftExpressions.Length
                        )
                        .ToArray();
            }

            if (rightExpressions != null
                && leftConstantExpression != null
                && leftConstantExpression.Value == null)
            {
                leftExpressions
                    = Enumerable
                        .Repeat<Expression>(
                            leftConstantExpression, 
                            rightExpressions.Length
                        )
                        .ToArray();
            }

            if (leftExpressions != null
                && rightExpressions != null
                && leftExpressions.Length == rightExpressions.Length)
            {
                if (leftExpressions.Length == 1
                    && kind == ExpressionType.Equal)
                {
                    var translatedExpression = TransformNullComparison(
                        leftExpressions[0], 
                        rightExpressions[0], 
                        kind
                    ) ?? Expression.Equal(leftExpressions[0], rightExpressions[0]);

                    return Expression.AndAlso(
                        translatedExpression, 
                        Expression.Constant(true, typeof(bool))
                    );
                }

                return leftExpressions
                    .Zip(
                        rightExpressions, (l, r) =>
                            TransformNullComparison(l, r, kind)
                            ?? Expression.MakeBinary(kind, l, r))
                    .Aggregate(
                        (e1, e2) =>
                            kind == ExpressionType.Equal
                                ? Expression.AndAlso(e1, e2)
                                : Expression.OrElse(e1, e2));
            }

            return expression;
        }
        
    }
}