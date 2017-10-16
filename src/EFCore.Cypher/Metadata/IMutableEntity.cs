using System.Collections.Generic;
using JetBrains.Annotations;

namespace Microsoft.EntityFrameworkCore.Metadata
{
    public interface IMutableEntity: IEntity, IMutableNode {

        /// <summary>
        /// Add unique constraint
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        IMutableConstraint AddUniqueConstraint([NotNull] IMutableNodeProperty property);

        /// <summary>
        /// Remove unique constraint
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        IMutableConstraint RemoveUniqueConstraint([NotNull] INodeProperty property);

        /// <summary>
        /// Add keys constraint (implicit exists assertion)
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        IMutableConstraint AddKeysConstraint([NotNull] IReadOnlyList<IMutableNodeProperty> properties);

        /// <summary>
        /// Remove keys constraint (implicit exists assertion)
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        IMutableConstraint RemoveKeysConstraint([NotNull] IReadOnlyList<INodeProperty> properties);

        /// <summary>
        /// Relationships (both inbound and outbound)
        /// </summary>
        /// <returns></returns>
        new IEnumerable<IMutableRelationship> GetRelationships();
    }
}