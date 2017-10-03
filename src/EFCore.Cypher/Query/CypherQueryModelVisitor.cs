using System.Collections.Generic;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
using Remotion.Linq;
using Remotion.Linq.Clauses;

namespace Microsoft.EntityFrameworkCore.Query {

    public class CypherQueryModelVisitor : EntityQueryModelVisitor
    {
        /// <summary>
        /// Cypher read parts can have multiple reading clauses (expressions)
        /// </summary>
        /// <returns></returns>
        protected virtual Dictionary<IQuerySource, ReadingExpression> QueriesBySource { get; } =
            new Dictionary<IQuerySource, ReadingExpression>();

        /// <summary>
        /// Visitor factory
        /// </summary>    
        private readonly ICypherTranslatingExpressionVisitorFactory _cypherTranslatingExpressionVisitorFactory;
        
        public CypherQueryModelVisitor(
            [NotNullAttribute] EntityQueryModelVisitorDependencies dependencies,
            // TODO: Specific cypher dependencies 
            [NotNullAttribute] QueryCompilationContext queryCompilationContext
            // TODO: Match Expression
        ) : base(dependencies, queryCompilationContext)
        {
            // TODO: Unwrap dependencies
        }

        /// <summary>
        /// Active reading expressions
        /// </summary>
        public virtual ICollection<ReadingExpression> Queries => QueriesBySource.Values;

        /// <summary>
        /// Get active ReadingExpression
        /// </summary>
        /// <param name="querySource"></param>
        /// <returns></returns>
        public virtual ReadingExpression TryGetQuery([NotNull] IQuerySource querySource) {
            // TODO: What happens when no reading expression is found?
            QueriesBySource.TryGetValue(querySource, out ReadingExpression expr);

            return expr;
        }

        /// <summary>
        /// Walk query model
        /// </summary>
        /// <param name="queryModel"></param>
        public override void VisitQueryModel(QueryModel queryModel) {
            base.VisitQueryModel(queryModel);

            // TODO: Fold Matches & Predicates & Handle wrapped queries (lifting/purging to QueriesBySource)
        }

        public override void VisitAdditionalFromClause(
            AdditionalFromClause fromClause,
            QueryModel queryModel,
            int index)
        {
            // TODO: Fold SelectMany (e.g. unwinds)
            base.VisitAdditionalFromClause(fromClause, queryModel, index);
        }

        /// <summary>
        /// Wrapped queries
        /// </summary>
        /// <param name="queryModel"></param>
        public virtual void VisitSubQueryModel([NotNull] QueryModel queryModel) {
            // TODO
        }

        /// <summary>
        /// Compile main from clause
        /// </summary>
        /// <param name="mainFromClause"></param>
        /// <param name="queryModel"></param>
        /// <returns></returns>
        protected override Expression CompileMainFromClauseExpression(
            MainFromClause mainFromClause,
            QueryModel queryModel)
        {
            // TODO: If mainFromClause is a wrapped query then lift otherwise pass to base
            return base.CompileMainFromClauseExpression(mainFromClause, queryModel);
        }

        /// <summary>
        /// Compile additional from clause
        /// </summary>
        /// <param name="additionalFromClause"></param>
        /// <param name="queryModel"></param>
        /// <returns></returns>
        protected override Expression CompileAdditionalFromClauseExpression(
            AdditionalFromClause additionalFromClause,
            QueryModel queryModel)
        {
            // TODO: If additional is a wrapped query then lift otherwise pass to base
            return base.CompileAdditionalFromClauseExpression(additionalFromClause, queryModel);
        }
    }
}