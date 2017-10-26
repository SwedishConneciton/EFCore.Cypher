using System;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal
{
    public interface IEntityAddedConvention {
        InternalEntityBuilder Apply([NotNull] InternalEntityBuilder builder);
    }
}