namespace Netfuser.Core.Rng
{
    /// <summary>
    /// Base class for the pseudo-random number generator events
    /// </summary>
    public abstract class RngEvent : NetfuserEvent
    {
        protected RngEvent(IContext context)
            : base(context)
        {
        }

        /// <summary>
        /// This event is fired when new pseudo-random generator is created.
        /// Observe to change the default seed, may need for debugging purposes
        /// to generate predictable sequences
        /// </summary>
        public class Create : RngEvent
        {
            /// <summary>
            /// Name of PRNG to be created
            /// </summary>
            public readonly string Name;
            /// <summary>
            /// Seed for this PRNG, may be changed at will
            /// </summary>
            public string Seed;

            internal Create(IContext context, string name)
                : base(context)
            {
                Name = name;
            }
        }
    }
}