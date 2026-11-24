using System.Text.Json;
using System.Text.Json.Serialization;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;

namespace StateLandGovernance.Api.Security;

// Fresh per authorization check, including the existing post-read/transaction rechecks.
// No watcher/cache delay, account writes, browser data, or fallback after a file error.
public sealed class CentralComplaintGrantFile(CentralComplaintOptions options) : IComplaintCentralGrantSource
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record Document(string SchemaVersion, CentralComplaintGrant[] Grants);
    public async Task<CentralComplaintGrant[]> ReadAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.GrantFilePath))
        {
            if (options.Grants.Any(x=>x.Persistent)) throw new CentralComplaintUnavailableException();
            return options.Grants; // Existing explicitly timed mode remains supported.
        }
        if (!Path.IsPathFullyQualified(options.GrantFilePath) || options.Grants.Length!=0)
            throw new CentralComplaintUnavailableException();
        try
        {
            await using var file=new FileStream(options.GrantFilePath,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete,4096,true);
            if(file.Length is <=0 or >65536)throw new CentralComplaintUnavailableException();
            using var buffer=new MemoryStream();
            var bytes=new byte[4096];int length;
            while((length=await file.ReadAsync(bytes,ct))>0){if(buffer.Length+length>65536)throw new CentralComplaintUnavailableException();await buffer.WriteAsync(bytes.AsMemory(0,length),ct);}
            using var json=JsonDocument.Parse(buffer.ToArray());
            if(!UniqueProperties(json.RootElement))throw new CentralComplaintUnavailableException();
            var document=json.RootElement.Deserialize<Document>();
            if(document?.SchemaVersion!="c4-central-permissions/1" || document.Grants is not {Length: <=16})throw new CentralComplaintUnavailableException();
            if(document.Grants.Any(x=>x==null)||document.Grants.GroupBy(x=>x.GrantId).Any(x=>x.Count()!=1)||document.Grants.GroupBy(x=>x.ActorId).Any(x=>x.Count()!=1))throw new CentralComplaintUnavailableException();
            var digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
            return document.Grants.Select(x=>x with{SourceReference=x.SourceReference+";grant-document-sha256="+digest}).ToArray();
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {throw new CentralComplaintUnavailableException();}
    }
    private static bool UniqueProperties(JsonElement value)=>value.ValueKind switch
    {
        JsonValueKind.Object=>value.EnumerateObject().Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()==value.EnumerateObject().Count()&&value.EnumerateObject().All(x=>UniqueProperties(x.Value)),
        JsonValueKind.Array=>value.EnumerateArray().All(UniqueProperties),
        _=>true
    };
}
