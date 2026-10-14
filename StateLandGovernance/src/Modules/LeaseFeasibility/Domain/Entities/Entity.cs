namespace StateLandGovernance.LeaseFeasibility.Domain.Entities;

public abstract class Entity
{
    public Guid Id { get; protected set; }

    protected Entity()
    {
        Id = Guid.NewGuid();
    }

    protected Entity(Guid id)
    {
        Id = id;
    }
}
