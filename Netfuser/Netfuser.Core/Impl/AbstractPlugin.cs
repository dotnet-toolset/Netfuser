using System;
using System.Text;
using Base.Lang;
using Base.Logging;

namespace Netfuser.Core.Impl
{
    public abstract class AbstractPlugin : Disposable, IPlugin
    {
        private readonly IContextImpl _context;
        public ILogger Logger { get; }
        public IContextImpl Context => _context;

        protected AbstractPlugin(IContextImpl context)
        {
            _context = context;
            var ln = new StringBuilder(GetType().Name);
            if (this is INamedPlugin np)
                ln.Append('-').Append(np.Name);
            Logger = context.Logger.GetLogger(ln.ToString());
        }

        protected override void OnDispose()
        {
            ((ContextImpl) _context).Unregister(this);
        }

        public abstract class Subscribed : AbstractPlugin
        {
            private readonly IDisposable _subscription;

            protected Subscribed(IContextImpl context)
                : base(context)
            {
                _subscription = context.Subscribe(Handle);
            }

            protected override void OnDispose()
            {
                _subscription?.Dispose();
                base.OnDispose();
            }

            protected abstract void Handle(NetfuserEvent ev);
        }
    }
}