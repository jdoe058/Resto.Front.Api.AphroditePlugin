
using Resto.Front.Api.Data.Security;
using Resto.Front.Api.Exceptions;

using System;

namespace Resto.Front.Api.AphroditePlugin
{
    internal static class OperationServiceExtensions
    {
        private const string Pin = "12344321";

        public static ICredentials GetCredentials(this IOperationService operationService)
        {
            if (operationService == null)
                throw new ArgumentNullException(nameof(operationService));
            try
            {
                return operationService.AuthenticateByPin("12344321");
            }
            catch (AuthenticationException ex)
            {
                PluginContext.Log.Warn("Cannot authenticate. Check pin for plugin user.");
                throw;
            }
        }
    }
}
