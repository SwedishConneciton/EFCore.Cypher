// Based on https://github.com/aspnet/EntityFrameworkCore
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Query.Cypher;
using Microsoft.EntityFrameworkCore.Query.Sql;
using Microsoft.EntityFrameworkCore.Utilities;
using Microsoft.EntityFrameworkCore.Storage;
using Remotion.Linq.Clauses;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Internal;

namespace Microsoft.EntityFrameworkCore.Query.Expressions
{
    /// <summary>
    /// See https://github.com/opencypher/openCypher/blob/master/grammar/cypher.xml
    /// 
    /// Read only expression with a Reading clause like Match or Unwind which maps 
    /// to a Relinq QuerySource then a Return.  Unfolding a Match might have an optional 
    /// match, hints and a predicate.  Returns can have a distinct aggregation.
    /// 
    /// A Cypher read only expression can contain multiple reading clauses.
    /// </summary>
    public class ReadOnlyExpression: NodeExpressionBase {

        private static readonly ExpressionEqualityComparer _expressionEqualityComparer = new ExpressionEqualityComparer();
        
        private readonly CypherQueryCompilationContext _queryCompliationContext;

        private readonly Dictionary<MemberInfo, Expression> _memberInfoProjectionMapping = new Dictionary<MemberInfo, Expression>();

        private readonly List<Expression> _returnItems = new List<Expression>();

        private readonly List<NodeExpressionBase> _readingClauses = new List<NodeExpressionBase>();

        private readonly List<Ordering> _order = new List<Ordering>();

        private Expression _limit;

        private Expression _skip;

        private bool _isReturnStar;

        private NodeExpressionBase _returnStarNode;

        private const string StorageAliasPrefix = "s";

        public ReadOnlyExpression(
            [NotNull] ReadOnlyExpressionDependencies dependencies,
            [NotNull] CypherQueryCompilationContext queryCompilationContext
        ): base(null, null) {
            Check.NotNull(dependencies, nameof(dependencies));
            Check.NotNull(queryCompilationContext, nameof(queryCompilationContext));
            
            Dependencies = dependencies;
            _queryCompliationContext = queryCompilationContext;
        }

        public ReadOnlyExpression(
            [NotNull] ReadOnlyExpressionDependencies dependencies,
            [NotNull] CypherQueryCompilationContext queryCompilationContext,
            [NotNull] string alias
        ) : this(dependencies, queryCompilationContext) {
            Check.NotNull(alias, nameof(alias));

            // TODO: Create unique alias
        }

        /// <summary>
        /// Dependencies
        /// </summary>
        /// <returns></returns>
        protected virtual ReadOnlyExpressionDependencies Dependencies { get; }

        /// <summary>
        /// Type of single read item or the type of the node expression base
        /// </summary>
        public override Type Type => _returnItems.Count == 1
            ? _returnItems[0].Type
            : base.Type;

        /// <summary>
        /// Visit the read items, reading clauses (plus predicates) and ordering expressions
        /// </summary>
        /// <param name="visitor"></param>
        /// <returns></returns>
        protected override Expression VisitChildren(ExpressionVisitor visitor) {
            foreach (var readItem in ReturnItems) {
                visitor.Visit(readItem);
            }

            foreach (var readingClause in ReadingClauses) {
                visitor.Visit(readingClause);
            }

            foreach (var ordering in Order) {
                visitor.Visit(ordering.Expression);
            }

            return this;
        }

        /// <summary>
        /// Return items (projections)
        /// </summary>
        public virtual IReadOnlyList<Expression> ReturnItems => _returnItems;

        /// <summary>
        /// Reading clauses (match, unwind etc.) which including the where clause (predicate)
        /// </summary>
        public virtual IReadOnlyList<NodeExpressionBase> ReadingClauses => _readingClauses;

        /// <summary>
        /// Order clause
        /// </summary>
        public virtual IReadOnlyList<Ordering> Order => _order;

        /// <summary>
        /// Limit
        /// </summary>
        /// <returns></returns>
        public virtual Expression Limit {
            get => _limit;

            [param: CanBeNull]
            set {
                // TODO: if limit is already set
                _limit = value;
            }
        }

        /// <summary>
        /// Skip
        /// </summary>
        /// <returns></returns>
        public virtual Expression Skip {
            get => _skip;

            [param: CanBeNull]
            set {
                // TODO: if limit is already set
                _skip = value;
            }
        }

        /// <summary>
        /// Dispatch (cypher expression visitor)
        /// </summary>
        /// <param name="visitor"></param>
        /// <returns></returns>
        protected override Expression Accept(ExpressionVisitor visitor) {
            Check.NotNull(visitor, nameof(visitor));

            var concrete = visitor as ICypherExpressionVisitor;

            return concrete is null
                ? base.Accept(visitor)
                : concrete.VisitReadOnly(this);
        }

        /// <summary>
        /// SQL generator (really cypher used by the shaper command context)
        /// </summary>
        /// <returns></returns>
        public virtual IQuerySqlGenerator CreateDefaultQueryCypherGenerator() => 
            Dependencies.QueryCypherGeneratorFactory.CreateDefault(this);

        /// <summary>
        /// Return everything (star)
        /// </summary>
        /// <returns></returns>
        public virtual bool IsReturnStar {
            get => _isReturnStar;

            set {
                _isReturnStar = value;

                // TODO: add range or clear
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<Type> GetReturnTypes() {
            if (_returnItems.Any() || !IsReturnStar) {
                return _returnItems
                    .Select(i => i.NodeType == ExpressionType.Convert && i.Type == typeof(object)
                        ? ((UnaryExpression)i).Operand.Type
                        : i.Type
                    );
            }

            return _readingClauses
                .OfType<ReadOnlyExpression>()
                .SelectMany(r => r.GetReturnTypes());
        }

        /// <summary>
        /// Add reading clause
        /// </summary>
        /// <param name="nodeExpression"></param>
        public virtual void AddReadingClause(
            [NotNull] NodeExpressionBase nodeExpression
        ) {
            Check.NotNull(nodeExpression, nameof(nodeExpression));

            _readingClauses.Add(nodeExpression);
        }

        /// <summary>
        /// Add return item
        /// </summary>
        /// <param name="property"></param>
        /// <param name="querySource"></param>
        /// <returns></returns>
        public virtual int AddReturnItem(
            [NotNull] IProperty property,
            [NotNull] IQuerySource querySource
        ) {
            Check.NotNull(property, nameof(property));
            Check.NotNull(querySource, nameof(querySource));

            return AddReturnItem(
                BindProperty(property, querySource)
            );
        }

        /// <summary>
        /// Add return item
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="resetReturnStart"></param>
        /// <returns></returns>
        public virtual int AddReturnItem(
            [NotNull] Expression expression,
            bool resetReturnStart = true
        ) {
            Check.NotNull(expression, nameof(expression));

            // convertion expressions
            if (expression.NodeType == ExpressionType.Convert) {
                var unary = (UnaryExpression)expression;

                if (unary.Type.UnwrapNullableType() == unary.Operand.Type) {
                    expression = unary.Operand;
                }
            }

            var returnItemIndex = _returnItems.FindIndex(
                e => _expressionEqualityComparer.Equals(e, expression)
                    || _expressionEqualityComparer.Equals((e as CypherAliasExpression)?.Expression, expression)
            );

            if (returnItemIndex != -1) {
                return returnItemIndex;
            }

            // TODO: when not a storage expression

            _returnItems.Add(expression);

            if (resetReturnStart) {
                IsReturnStar = false;
            }

            return _returnItems.Count - 1;
        }

        public virtual Expression BindProperty(
            [NotNull] IProperty property,
            [NotNull] IQuerySource querySource
        ) {
            Check.NotNull(property, nameof(property));
            Check.NotNull(querySource, nameof(querySource));

            var node = GetNodeForQuerySource(querySource);

            // TODO: Nested queries + joined entities

            return new StorageExpression(
                property.Cypher().StorageName,
                property,
                node
            );
        }

        /// <summary>
        /// Remove range from return (items)
        /// </summary>
        /// <param name="index"></param>
        public virtual void RemoveRangeFromReturn(int index)
        {
            if (index < _returnItems.Count)
            {
                _returnItems.RemoveRange(index, _returnItems.Count - index);
            }
        }

        /// <summary>
        /// Node for query source
        /// </summary>
        /// <param name="querySource"></param>
        /// <returns></returns>
        public virtual NodeExpressionBase GetNodeForQuerySource(
            [NotNull] IQuerySource querySource
        ) {
            Check.NotNull(querySource, nameof(querySource));

            return _readingClauses
                .FirstOrDefault(rc => rc.QuerySource == querySource)
                ?? ReturnStarNode;
        }

        public virtual Expression GetReturnForMemberInfo(
            [NotNull] MemberInfo memberInfo
        ) {
            Check.NotNull(memberInfo, nameof(memberInfo));

            return _memberInfoProjectionMapping.ContainsKey(memberInfo)
                ? _memberInfoProjectionMapping[memberInfo]
                : null;
        }

        public virtual void SetReturnForMemberInfo(
            [NotNull] MemberInfo memberInfo, 
            [NotNull] Expression projection
        ) {
            Check.NotNull(memberInfo, nameof(memberInfo));
            Check.NotNull(projection, nameof(projection));

            _memberInfoProjectionMapping[memberInfo] = CreateUniqueReturn(
                projection,
                memberInfo.Name
            );
        }

        /// <summary>
        /// Node for star return
        /// </summary>
        /// <returns></returns>
        public virtual NodeExpressionBase ReturnStarNode {
            get {
                return _returnStarNode ??
                    (_readingClauses.Count == 1 ? _readingClauses.Single(): null);
            }

            [param: CanBeNull]
            set {
                _returnStarNode = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
            => CreateDefaultQueryCypherGenerator()
                .GenerateSql(new Dictionary<string, object>())
                .CommandText;

        private Expression CreateUniqueReturn(
            Expression expression,
            string alias = null
        ) {
            var index = _returnItems.FindIndex(e => e.Equals(expression));
            if (index != -1) {
                _returnItems.RemoveAt(index);
            }

            var current = GetStorageName(expression);
            var uniqueBase = alias ?? current ?? StorageAliasPrefix; 
            var unique = uniqueBase;
            var counter = 0;

            while (_returnItems.Select(GetStorageName).Any(p => string.Equals(p, unique, StringComparison.Ordinal))) {
                unique = uniqueBase + counter++;
            }

            var updatedExpression
                = !string.Equals(
                    current, 
                    unique, 
                    StringComparison.OrdinalIgnoreCase
                )
                ? new CypherAliasExpression(
                    unique, 
                    (expression as CypherAliasExpression)?.Expression ?? expression
                )
                : expression;

            var currentOrderingIndex = _order.FindIndex(e => e.Expression.Equals(expression));

            if (currentOrderingIndex != -1)
            {
                // TODO:
            }

            if (index != -1) {
                _returnItems.Insert(index, updatedExpression);
            }

            return updatedExpression;
        }

        private static string GetStorageName(Expression expression) {
            expression = expression.RemoveConvert();
            expression = (expression as NullableExpression)?.Operand.RemoveConvert()
                         ?? expression;

            return (expression as CypherAliasExpression)?.Alias
                   ?? (expression as StorageExpression)?.Name;         
        }
    }
}