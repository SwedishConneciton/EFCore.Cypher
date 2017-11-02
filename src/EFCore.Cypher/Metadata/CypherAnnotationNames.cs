// Based on https://github.com/aspnet/EntityFrameworkCore
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.EntityFrameworkCore.Metadata
{
    public static class CypherAnnotationNames {

        /// <summary>
        /// Prefix associated with cypher annotations
        /// </summary>
        public const string Prefix = "Cypher:";

        /// <summary>
        /// Labels
        /// </summary>
        public const string Labels = Prefix + "Labels";
    }
}