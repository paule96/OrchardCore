using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OrchardCore.DisplayManagement
{
    public interface IAsyncViewActionFilter : IAsyncActionFilter
    {
        Task OnActionExecutionAsync(ActionContext context);
    }
}
