using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.UnitTests.LandIntelligence.Parcels;

internal sealed class InMemoryLandParcelRepository : ILandParcelRepository
{
    private readonly List<LandParcel> _parcels = [];

    public InMemoryLandParcelRepository(params LandParcel[] seedParcels)
    {
        _parcels.AddRange(seedParcels);
    }

    public Task<LandParcel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_parcels.FirstOrDefault(parcel => parcel.Id == id));

    public Task<LandParcel?> GetByCadastralNumberAsync(
        string cadastralNumber,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_parcels.FirstOrDefault(parcel =>
            parcel.Identifier.CadastralNumber.Equals(cadastralNumber, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<LandParcel>> SearchAsync(
        LandSearchRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LandParcel>>([.. _parcels]);

    public Task<int> CountSearchAsync(
        LandSearchRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_parcels.Count);

    public Task AddAsync(LandParcel parcel, CancellationToken cancellationToken = default)
    {
        _parcels.Add(parcel);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LandParcel parcel, CancellationToken cancellationToken = default)
    {
        var index = _parcels.FindIndex(existing => existing.Id == parcel.Id);
        if (index >= 0)
        {
            _parcels[index] = parcel;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _parcels.RemoveAll(parcel => parcel.Id == id);
        return Task.CompletedTask;
    }
}
