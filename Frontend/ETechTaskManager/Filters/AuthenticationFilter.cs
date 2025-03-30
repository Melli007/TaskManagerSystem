using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ETechTaskManager.Filters
{
    public class AuthenticationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var userRole = context.HttpContext.Session.GetString("UserRole");
            var controller = (string)context.RouteData.Values["controller"];
            var action = (string)context.RouteData.Values["action"];

            // If the user is not authenticated and not on the login page
            if (string.IsNullOrEmpty(userRole) && !(controller == "Home" && action == "Login"))
            {
                // Redirect to the login page if the user is not authenticated
                context.Result = new RedirectToActionResult("Login", "Home", null);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Optional: You can handle post-action logic here if needed
        }
    }
}
