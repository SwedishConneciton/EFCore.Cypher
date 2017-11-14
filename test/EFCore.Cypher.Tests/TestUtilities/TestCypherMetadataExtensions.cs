// Based on https://github.com/aspnet/EntityFrameworkCore
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore.TestUtilities
{
    public static class TestCypherMetadataExtensions
    {
        public static IRelationalPropertyAnnotations TestProvider(this IProperty property)
            => new RelationalPropertyAnnotations(property);

        public static IRelationalEntityTypeAnnotations TestProvider(this IEntityType entityType)
            => new RelationalEntityTypeAnnotations(entityType);

        public static IRelationalKeyAnnotations TestProvider(this IKey key)
            => new RelationalKeyAnnotations(key);

        public static IRelationalIndexAnnotations TestProvider(this IIndex index)
            => new RelationalIndexAnnotations(index);

        public static IRelationalForeignKeyAnnotations TestProvider(this IForeignKey foreignKey)
            => new RelationalForeignKeyAnnotations(foreignKey);

        public static IRelationalModelAnnotations TestProvider(this IModel model)
            => new RelationalModelAnnotations(model);
    }
}