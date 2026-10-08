using System.Reflection;
using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

internal static class PersistenceEntityIdHelper
{
    public static TEntity SetEntityId<TEntity>(TEntity entity, Guid id)
        where TEntity : Entity
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(entity, id);

        return entity;
    }

    public static void SetProperty<T>(T instance, string propertyName, object? value)
    {
        typeof(T)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }
}
