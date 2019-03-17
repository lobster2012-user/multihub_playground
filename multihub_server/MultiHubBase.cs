﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace multihub_server
{

    public abstract class MultiHubBase<TMulti> : Hub<TMulti>
        where TMulti : class
    {
        private readonly IServiceProvider _provider;
        private HashSet<string> _enabledHubNames;
        protected ILogger _logger { get; private set; }

        protected abstract IEnumerable<Hub> GetHubs();

        protected MultiHubBase(ILogger<MultiHub> logger, IServiceProvider provider)
        //     HubConnectionContext context)
        {
            logger.LogDebug("****** MultiHub-ctor");
            _provider = provider;
            _logger = logger;
            _logger.LogDebug("context");
            //   Console.WriteLine(_hubs);
            //   Console.WriteLine(_hubs);
        }

        protected bool IsHubEnabled<T>()
        {
            return GetEnabledHubNames().Contains(typeof(T).Name);
        }
        protected void CheckHub<TClient>()
        {
            if (!IsHubEnabled<TClient>())
            {
                throw new InvalidOperationException("not enabled hub");
            }
        }

        private HashSet<string> GetEnabledHubNames()
        {
            if (_enabledHubNames != null)
                return _enabledHubNames;
            var query = this.Context.GetHttpContext().Request.Query;
            if (query.ContainsKey("hubs"))
            {
                return (_enabledHubNames =
                    new HashSet<string>(query["hubs"].ToArray().SelectMany(z => z.Split(',', '+')).Select(z => z.Trim()).Where(z => z.Length > 0)));
            }
            return (_enabledHubNames = new HashSet<string>());
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogDebug("****** on-connected");
            await Task.WhenAll(GetHubs().Select(z => z.OnConnectedAsync()).ToArray());
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogDebug("****** on-disconnected");
            await Task.WhenAll(GetHubs().Select(z => z.OnDisconnectedAsync(exception)).ToArray());
            await base.OnDisconnectedAsync(exception);
        }

        protected override void Dispose(bool disposing)
        {
            _logger.LogDebug("****** MultiHub-dispose");
            if (disposing)
            {
                foreach (var hub in GetHubs())
                {
                    hub.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        ///             //USE SCOPED HUB ACTIVATOR ????
        /// </summary>
        /// <typeparam name="THub"></typeparam>
        /// <typeparam name="TClient"></typeparam>
        /// <typeparam name="TMultiClient"></typeparam>
        /// <param name="src"></param>
        /// <returns></returns>

        private THub CreateHubFrom<THub, TClient, TMultiClient>(MultiHubBase<TMultiClient> src)
            where THub : Hub<TClient>
            where TClient : class
            where TMultiClient : class, TClient
        {
            _logger.LogDebug($"****** CreateHubFrom {typeof(THub).Name}");

            src.CheckHub<THub>();
            var hub = _provider.GetRequiredService<THub>();
            hub.Groups = src.Groups;
            hub.Context = src.Context;
            hub.Clients = new MultiHubCallerClientsToConcrete<TMultiClient, TClient>(src.Clients);
            return hub;
        }
    
        protected THub CreateHub<THub, TClient>()
        {
            return this.CreateHub<THub, TClient, TMulti>();
        }        
    }  

}
