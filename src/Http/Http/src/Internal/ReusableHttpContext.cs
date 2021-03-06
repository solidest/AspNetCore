using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;

namespace Microsoft.AspNetCore.Http.Internal
{
    public sealed class ReusableHttpContext : HttpContext
    {
        // Lambdas hoisted to static readonly fields to improve inlining https://github.com/dotnet/roslyn/issues/13624
        private readonly static Func<IFeatureCollection, IItemsFeature> _newItemsFeature = f => new ItemsFeature();
        private readonly static Func<IFeatureCollection, IServiceProvidersFeature> _newServiceProvidersFeature = f => new ServiceProvidersFeature();
        private readonly static Func<IFeatureCollection, IHttpAuthenticationFeature> _newHttpAuthenticationFeature = f => new HttpAuthenticationFeature();
        private readonly static Func<IFeatureCollection, IHttpRequestLifetimeFeature> _newHttpRequestLifetimeFeature = f => new HttpRequestLifetimeFeature();
        private readonly static Func<IFeatureCollection, ISessionFeature> _newSessionFeature = f => new DefaultSessionFeature();
        private readonly static Func<IFeatureCollection, ISessionFeature> _nullSessionFeature = f => null;
        private readonly static Func<IFeatureCollection, IHttpRequestIdentifierFeature> _newHttpRequestIdentifierFeature = f => new HttpRequestIdentifierFeature();

        private FeatureReferences<FeatureInterfaces> _features;

        private ReusableHttpRequest _request;
        private ReusableHttpResponse _response;

        private ReusableConnectionInfo _connection;
        private ReusableWebSocketManager _websockets;

        public ReusableHttpContext(IFeatureCollection features)
        {
            _features = new FeatureReferences<FeatureInterfaces>(features);
            _request = new ReusableHttpRequest(this);
            _response = new ReusableHttpResponse(this);
        }

        public void Initialize(IFeatureCollection features)
        {
            _features = new FeatureReferences<FeatureInterfaces>(features);
            _request.Initialize(this);
            _response.Initialize(this);
            _connection?.Initialize(features);
            _websockets?.Initialize(features);
        }

        public void Uninitialize()
        {
            _features = default;

            _request.Uninitialize();
            _response.Uninitialize();
            _connection?.Uninitialize();
            _websockets?.Uninitialize();
        }

        private IItemsFeature ItemsFeature =>
            _features.Fetch(ref _features.Cache.Items, _newItemsFeature);

        private IServiceProvidersFeature ServiceProvidersFeature =>
            _features.Fetch(ref _features.Cache.ServiceProviders, _newServiceProvidersFeature);

        private IHttpAuthenticationFeature HttpAuthenticationFeature =>
            _features.Fetch(ref _features.Cache.Authentication, _newHttpAuthenticationFeature);

        private IHttpRequestLifetimeFeature LifetimeFeature =>
            _features.Fetch(ref _features.Cache.Lifetime, _newHttpRequestLifetimeFeature);

        private ISessionFeature SessionFeature =>
            _features.Fetch(ref _features.Cache.Session, _newSessionFeature);

        private ISessionFeature SessionFeatureOrNull =>
            _features.Fetch(ref _features.Cache.Session, _nullSessionFeature);


        private IHttpRequestIdentifierFeature RequestIdentifierFeature =>
            _features.Fetch(ref _features.Cache.RequestIdentifier, _newHttpRequestIdentifierFeature);

        public override IFeatureCollection Features => _features.Collection;

        public override HttpRequest Request => _request;

        public override HttpResponse Response => _response;

        public override ConnectionInfo Connection => _connection ?? (_connection = new ReusableConnectionInfo(_features.Collection));
        
        [Obsolete("This is obsolete and will be removed in a future version. The recommended alternative is to use Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions. See https://go.microsoft.com/fwlink/?linkid=845470.")]
        public override AuthenticationManager Authentication => throw new NotSupportedException();

        public override WebSocketManager WebSockets => _websockets ?? (_websockets = new ReusableWebSocketManager(_features.Collection));


        public override ClaimsPrincipal User
        {
            get
            {
                var user = HttpAuthenticationFeature.User;
                if (user == null)
                {
                    user = new ClaimsPrincipal(new ClaimsIdentity());
                    HttpAuthenticationFeature.User = user;
                }
                return user;
            }
            set { HttpAuthenticationFeature.User = value; }
        }

        public override IDictionary<object, object> Items
        {
            get { return ItemsFeature.Items; }
            set { ItemsFeature.Items = value; }
        }

        public override IServiceProvider RequestServices
        {
            get { return ServiceProvidersFeature.RequestServices; }
            set { ServiceProvidersFeature.RequestServices = value; }
        }

        public override CancellationToken RequestAborted
        {
            get { return LifetimeFeature.RequestAborted; }
            set { LifetimeFeature.RequestAborted = value; }
        }

        public override string TraceIdentifier
        {
            get { return RequestIdentifierFeature.TraceIdentifier; }
            set { RequestIdentifierFeature.TraceIdentifier = value; }
        }

        public override ISession Session
        {
            get
            {
                var feature = SessionFeatureOrNull;
                if (feature == null)
                {
                    throw new InvalidOperationException("Session has not been configured for this application " +
                        "or request.");
                }
                return feature.Session;
            }
            set
            {
                SessionFeature.Session = value;
            }
        }

        public override void Abort()
        {
            LifetimeFeature.Abort();
        }

        struct FeatureInterfaces
        {
            public IItemsFeature Items;
            public IServiceProvidersFeature ServiceProviders;
            public IHttpAuthenticationFeature Authentication;
            public IHttpRequestLifetimeFeature Lifetime;
            public ISessionFeature Session;
            public IHttpRequestIdentifierFeature RequestIdentifier;
        }
    }
}
