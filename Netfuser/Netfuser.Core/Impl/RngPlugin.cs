using System;
using System.Collections.Concurrent;
using System.Text;
using Base.Rng;
using Netfuser.Core.Rng;

namespace Netfuser.Core.Impl
{
    class RngPlugin : AbstractPlugin.Subscribed, IRngPlugin
    {
        private readonly ConcurrentDictionary<string, IRng> _rngs;
        private string _seed;

        internal RngPlugin(IContextImpl context)
            : base(context)
        {
            _rngs = new ConcurrentDictionary<string, IRng>();
        }


        public IRng Get(string name) =>
            _rngs.GetOrAdd(name, n =>
            {
                var sb = new StringBuilder(_seed);
                if (name != null) sb.Append('@').Append(name);
                var seed = Context.Fire(new RngEvent.Create(Context, name) { Seed = sb.ToString() }).Seed;
                return RngFactory.CreateSeeded(seed);
            });

        protected override void Handle(NetfuserEvent ev)
        {
            switch (ev)
            {
                case NetfuserEvent.Initialize _:
                    _seed = Context.Fire(new RngEvent.Create(Context, null) { Seed = Guid.NewGuid().ToString() }).Seed;
                    break;
            }
        }
    }
}