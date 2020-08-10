using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace BlockDX.Api.ExceptionHandling
{
    public class JsonExceptionMiddleware
    {
        public JsonExceptionMiddleware()
        {
        }

        public async Task Invoke(HttpContext context)
        {
            var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
            if (contextFeature != null && contextFeature.Error != null)
            {
                // ...
                // Add lines to your log file, or your 
                // Application insights instance here
                // ...
                context.Response.StatusCode = (int)GetErrorCode(contextFeature.Error);
                context.Response.ContentType = "application/json";

                //var jsonRpcError = new JsonRpcError
                //{
                //    Message = contextFeature.Error.Message
                //};
                //var rpcErrorCodeProperty = contextFeature.Error.InnerException.GetType().GetProperty("RpcErrorCode");
                //if (rpcErrorCodeProperty != null)
                //{
                //    var rpcErrorCode = (RpcErrorCode)rpcErrorCodeProperty.GetValue(contextFeature.Error.InnerException);
                //    jsonRpcError.Code = rpcErrorCode;
                //}

                await context.Response.WriteAsync(JsonConvert.SerializeObject(contextFeature.Error.InnerException.Data["rpcResponse"]));
            }
        }

        private static HttpStatusCode GetErrorCode(Exception e)
        {
            switch (e)
            {
                case ValidationException _:
                    return HttpStatusCode.BadRequest;
                case FormatException _:
                    return HttpStatusCode.BadRequest;
                case AuthenticationException _:
                    return HttpStatusCode.Forbidden;
                case NotImplementedException _:
                    return HttpStatusCode.NotImplemented;
                default:
                    return HttpStatusCode.InternalServerError;
            }
        }
    }
}
