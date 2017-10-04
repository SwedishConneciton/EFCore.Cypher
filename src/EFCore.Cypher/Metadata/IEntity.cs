

using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.Metadata
{
    public interface IEntity: INode {        
        /// <summary>
        /// Relationships (both inbound and outbound)
        /// </summary>
        /// <returns></returns>
        IEnumerable<IRelationship> GetRelationships();
    }
}