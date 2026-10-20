using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using StateLandGovernance.WorkflowGovernance.Presentation.Controllers;
namespace StateLandGovernance.UnitTests.WorkflowGovernance.Presentation;
public sealed class CaseAccessStatusTests
{
    [Theory] [InlineData(false,401)] [InlineData(true,403)]
    public void DeniedResultDistinguishesSignInFromAssignment(bool authenticated, int expected)
    {
        var http=new DefaultHttpContext();
        if(authenticated) http.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("sub",Guid.NewGuid().ToString())},"TEST-ONLY"));
        var context=new ResultExecutingContext(new ActionContext(http,new RouteData(),new ActionDescriptor()),new List<IFilterMetadata>(),new StatusCodeResult(403),new object());
        new CaseManagementExceptionFilterAttribute().OnResultExecuting(context);
        var result=Assert.IsType<ObjectResult>(context.Result); Assert.Equal(expected,result.StatusCode);
        Assert.True(Assert.IsType<ProblemDetails>(result.Value).Extensions.ContainsKey("traceId"));
    }
}
