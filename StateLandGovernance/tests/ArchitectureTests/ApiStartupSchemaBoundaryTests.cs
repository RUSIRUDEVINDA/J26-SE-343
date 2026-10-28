using System.Reflection;
using System.Reflection.Emit;
namespace StateLandGovernance.ArchitectureTests;
public sealed class ApiStartupSchemaBoundaryTests
{
    [Fact] public void CompiledStartup_HasNoMigrationSchemaCreationOrRawSqlCalls()
    {
        var assembly=typeof(StateLandGovernance.Api.Security.DevelopmentAccounts).Assembly;
        var types=assembly.GetTypes().Where(t=>t.Name=="Program"||t.FullName?.StartsWith("Program+",StringComparison.Ordinal)==true);
        var codes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(c=>unchecked((ushort)c.Value));
        var calls=new List<MethodBase>();
        foreach(var method in types.SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance)))
        {
            var bytes=method.GetMethodBody()?.GetILAsByteArray();if(bytes==null)continue;
            for(var i=0;i<bytes.Length;)
            {
                ushort value=bytes[i++];if(value==0xfe)value=(ushort)(0xfe00|bytes[i++]);var code=codes[value];
                if(code.OperandType==OperandType.InlineMethod)
                {
                    try{if(method.Module.ResolveMethod(BitConverter.ToInt32(bytes,i)) is {} called)calls.Add(called);}catch(ArgumentException){}
                }
                i+=code.OperandType switch{
                    OperandType.InlineNone=>0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,
                    OperandType.InlineVar=>2,
                    OperandType.InlineI8 or OperandType.InlineR=>8,
                    OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(bytes,i),
                    _=>4};
            }
        }
        Assert.NotEmpty(calls);
        Assert.DoesNotContain(calls,c=>c.Name is "Migrate" or "MigrateAsync" or "EnsureCreated" or "EnsureCreatedAsync" or "EnsureDeleted" or "EnsureDeletedAsync" or "ExecuteSqlRaw" or "ExecuteSqlRawAsync" or "ExecuteSqlInterpolated" or "ExecuteSqlInterpolatedAsync" or "SaveChanges" or "SaveChangesAsync");
    }
}
