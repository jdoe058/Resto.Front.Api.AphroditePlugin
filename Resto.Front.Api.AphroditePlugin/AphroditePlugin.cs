
using Resto.Front.Api.Attributes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;


namespace Resto.Front.Api.AphroditePlugin
{
    [PluginLicenseModuleId(21016318)]

    public sealed class AphroditePlugin : IFrontPlugin, IDisposable
    {
        private readonly Stack<IDisposable> subscriptions = new Stack<IDisposable>();

        public void Dispose()
        {
            while (this.subscriptions.Any<IDisposable>())
            {
                IDisposable disposable = this.subscriptions.Pop();
                try
                {
                    disposable.Dispose();
                }
                catch (RemotingException)
                {
                }
            }
            PluginContext.Log.Info("AphroditePlugin stopped");
        }

        public AphroditePlugin()
        {
            PluginContext.Log.Info("Initializing AphroditePlugin");
            this.subscriptions.Push((IDisposable)new Transactions());
        }
    }
}
