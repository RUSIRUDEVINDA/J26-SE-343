namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
/// Infrastructure rehydrator for pure Domain entities that lack parameterless or persistence constructors.
/// Restores exact committed domain state without invoking business logic or firing domain events.
/// </summary>
internal static class AggregateRehydrator
{
    private static readonly ConcurrentDictionary<(Type, string), FieldInfo?> FieldCache = new();

    public static T Create<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    public static object Create(Type type)
    {
        return RuntimeHelpers.GetUninitializedObject(type);
    }

    public static void SetField<T>(T target, string fieldName, object? value) where T : class
    {
        ArgumentNullException.ThrowIfNull(target);

        var targetType = target.GetType();
        var key = (targetType, fieldName);
        var field = FieldCache.GetOrAdd(key, k => FindField(k.Item1, k.Item2));

        if (field == null)
        {
            throw new InvalidOperationException($"Field '{fieldName}' not found on type '{targetType.FullName}'.");
        }

        field.SetValue(target, value);
    }

    public static void SetPropertyBackingField<T>(T target, string propertyName, object? value) where T : class
    {
        SetField(target, $"<{propertyName}>k__BackingField", value);
    }

    private static FieldInfo? FindField(Type type, string fieldName)
    {
        Type? current = type;
        while (current != null && current != typeof(object))
        {
            var field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) return field;
            current = current.BaseType;
        }
        return null;
    }
}
