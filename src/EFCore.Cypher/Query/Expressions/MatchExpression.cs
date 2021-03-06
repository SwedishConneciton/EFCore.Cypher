// Based on https://github.com/aspnet/EntityFrameworkCore
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Query.Cypher;
using Microsoft.EntityFrameworkCore.Utilities;
using Remotion.Linq.Clauses;

namespace Microsoft.EntityFrameworkCore.Query.Expressions {
    public class MatchExpression : ReadingClause, IEquatable<MatchExpression>
    {
        public MatchExpression(
            [NotNull] PatternExpression pattern
        ) {
            Check.NotNull(pattern, nameof(pattern));

            Pattern = pattern;
        }

        /// <summary>
        /// Pattern
        /// </summary>
        /// <returns></returns>
        public virtual PatternExpression Pattern { get; }

        /// <summary>
        /// Where (predicate)
        /// </summary>
        /// <returns></returns>
        public virtual Expression Where { 
            get; 
            
            [param: CanBeNull] 
            set; 
        }

        /// <summary>
        /// Optional match
        /// </summary>
        /// <returns></returns>
        public virtual bool Optional {
            get; 

            [param: CanBeNull]
            set;
        }

        /// <summary>
        /// Append with AndAlso expression predicate to where clause
        /// </summary>
        /// <param name="where"></param>
        public virtual void AddToWhere([NotNull] Expression where) {
            Check.NotNull(where, nameof(where));

            Where = Where is null
                ? where 
                : AndAlso(Where, where);
        }

        /// <summary>
        /// Dispatcher
        /// </summary>
        /// <param name="visitor"></param>
        /// <returns></returns>
        protected override Expression Accept(ExpressionVisitor visitor)
        {
            Check.NotNull(visitor, nameof(visitor));

            var concrete = visitor as ICypherExpressionVisitor;

            return concrete is null
                ? base.Accept(visitor)
                : concrete.VisitMatch(this);
        }

        /// <summary>
        /// Visit children
        /// </summary>
        /// <param name="visitor"></param>
        /// <returns></returns>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            visitor.Visit(Pattern);
            visitor.Visit(Where);

            return this;
        }

        /// <summary>
        /// Equals
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(MatchExpression other)
            => Equals(Pattern, other.Pattern)
                && Optional == other.Optional
                && Equals(Where, other.Where);

        /// <summary>
        /// Equals (object)
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }

            if (ReferenceEquals(this, obj)) {
                return true;
            }

            if (obj.GetType() != GetType()) {
                return false;
            }

		    return Equals(obj as MatchExpression);
        }

        /// <summary>
        /// Hash
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() {
            unchecked
            {
                var hashCode = Pattern.GetHashCode();
                hashCode = (hashCode * 397) ^ Optional.GetHashCode();
                hashCode = (hashCode * 397) ^ Where?.GetHashCode() ?? 0;

                return hashCode;
            }
        }

        /// <summary>
        /// Human readable
        /// </summary>
        /// <returns></returns>
        public override string ToString()
            => (Optional ? "Optional" : String.Empty) + 
                Pattern + 
                (Where is null ? String.Empty : " WHERE " + Where);
    }
}