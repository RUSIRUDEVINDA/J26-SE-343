using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StateLandGovernance.Api.Security;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Presentation.Security;
using Xunit;
public sealed class DevelopmentAccountsTests
{
 private static readonly Guid Actor=Guid.NewGuid();
 private static IConfiguration Config(bool enabled=true){var salt=new byte[16];return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
 ["LocalDevelopmentSignIn:Enabled"]=enabled.ToString(),["LocalDevelopmentSignIn:Accounts:0:Username"]="test-only",
 ["LocalDevelopmentSignIn:Accounts:0:ActorId"]=Actor.ToString(),["LocalDevelopmentSignIn:Accounts:0:TokenRole"]="Officer",
 ["LocalDevelopmentSignIn:Accounts:0:PasswordSalt"]=Convert.ToBase64String(salt),
 ["LocalDevelopmentSignIn:Accounts:0:PasswordHash"]=Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2("TEST-only",salt,100000,HashAlgorithmName.SHA256,32))}).Build();}
 private static DevelopmentAccounts Service(string environment="Development",bool enabled=true)=>new(Config(enabled),new Env(environment),new LandIntelligenceJwtTokenService(Options.Create(new LandIntelligenceAuthenticationOptions {JwtSigningKeyBase64=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))})));
 private static DefaultHttpContext Http(string ip="127.0.0.1"){var h=new DefaultHttpContext();h.Connection.RemoteIpAddress=System.Net.IPAddress.Parse(ip);return h;}
 [Fact] public void CredentialsAndTokenClaimsComeFromServerAccount(){var s=Service();Assert.Null(s.SignIn("test-only","wrong"));Assert.Null(s.SignIn("unknown","TEST-only"));var response=JsonSerializer.SerializeToElement(s.SignIn("test-only","TEST-only"));var jwt=new JwtSecurityTokenHandler().ReadJwtToken(response.GetProperty("accessToken").GetString());Assert.Contains(jwt.Claims,c=>c.Type=="development_account"&&c.Value=="test-only");Assert.Contains(jwt.Claims,c=>c.Type=="sub"&&c.Value==Actor.ToString());Assert.DoesNotContain(jwt.Claims,c=>c.Type=="capability");}
 [Fact] public void ProductionDisabledAndRemoteRejectDevelopmentTokens(){var dev=Service();var response=JsonSerializer.SerializeToElement(dev.SignIn("test-only","TEST-only"));var jwt=new JwtSecurityTokenHandler().ReadJwtToken(response.GetProperty("accessToken").GetString());var p=new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims,"test-token"));Assert.True(dev.Validate(Http(),p));Assert.False(dev.Validate(Http("192.0.2.10"),p));Assert.False(Service("Production").Validate(Http(),p));Assert.False(Service(enabled:false).Validate(Http(),p));Assert.False(dev.Validate(Http(),new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("sub",Actor.ToString())},"legacy"))));}
 [Fact] public void LiveSessionCannotInjectCapabilityRoleOrAnotherOwner(){var s=Service();var response=JsonSerializer.SerializeToElement(s.SignIn("test-only","TEST-only"));var jwt=new JwtSecurityTokenHandler().ReadJwtToken(response.GetProperty("accessToken").GetString());
 var id=new ClaimsIdentity(jwt.Claims,"test-token");var p=new ClaimsPrincipal(id);Assert.True(s.Validate(Http(),p));id.AddClaim(new("capability","WorkflowPlanApprover"));Assert.False(s.Validate(Http(),p));id.RemoveClaim(id.FindFirst("capability")!);id.AddClaim(new(ClaimTypes.Role,"Administrator"));Assert.False(s.Validate(Http(),p));id.RemoveClaim(id.FindAll(ClaimTypes.Role).Last());id.RemoveClaim(id.FindFirst("land_intelligence_owner_id")!);id.AddClaim(new("land_intelligence_owner_id",Guid.NewGuid().ToString()));Assert.False(s.Validate(Http(),p));}
 [Fact] public void SignOutRevokesTheActualSessionAndRestartAlsoInvalidates(){var s=Service();var response=JsonSerializer.SerializeToElement(s.SignIn("test-only","TEST-only"));var jwt=new JwtSecurityTokenHandler().ReadJwtToken(response.GetProperty("accessToken").GetString());var p=new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims,"test-token"));Assert.True(s.Validate(Http(),p));Assert.False(Service().Validate(Http(),p));s.SignOut(p);Assert.False(s.Validate(Http(),p));}
 private sealed class Env(string name):IHostEnvironment{public string EnvironmentName{get;set;}=name;public string ApplicationName{get;set;}="TEST-ONLY";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
