namespace Netfuser.Core.Manglers.Strings
{
    public interface IStringMangleMethod : INamedPlugin
    {
        /// <summary>
        /// Dequeues the next piece of the string from <see cref="IStringMangleContext.Pieces"/>, and emits code 
        /// that leaves either demangled piece on stack, or appends it to the <see cref="System.Text.StringBuilder"/>
        /// if that is being used on the <see cref="IStringMangleContext"/>
        /// </summary>
        /// <param name="context"></param>
        /// <returns> either <see cref="StringMangleStackTop.String"/> or <see cref="StringMangleStackTop.StringBuilder"/> </returns>
        StringMangleStackTop? Emit(IStringMangleContext context);
    }
}