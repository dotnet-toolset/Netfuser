using System.Linq;
using Base.Rng;
using Netfuser.Core.Manglers.ControlFlow;
using Netfuser.Dnext.Cil;

namespace Netfuser.Core.Impl.Manglers.ControlFlow.Methods
{
    class Jump : CFMangleMethod
    {
        public Jump(IContextImpl context) 
            : base(context, NetfuserFactory.CFManglerJumpName)
        {
        }

        public override void Mangle(ICFMangleContext context, Block.Regular block)
        {
            var fragments = context.SplitFragments(block);
            if (fragments.Count < 4) return;
            var current = fragments.First;
            while (current.Next != null)
            {
                var newFragment = new CilFragment(current.Value);
                context.AddJump(newFragment, current.Next.Value.First, true);
                current.Value = newFragment;
                current = current.Next;
            }

            var first = fragments.First.Value;
            fragments.RemoveFirst();
            var last = fragments.Last.Value;
            fragments.RemoveLast();

            var newFragments = fragments.ToList();
            newFragments.Shuffle(Mangler.Rng);

            block.Fragment.Reset(first
                .Concat(newFragments.SelectMany(fragment => fragment))
                .Concat(last));
        }

    }
}