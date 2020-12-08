using Netfuser.Core.Impl;

namespace Netfuser.Core
{
    public static class NetfuserFactory
    {
        public const string NetfuserName = "netfuser";

        public const string EmbedderName = "embedder";
        public const string MetadataManglerName = "metadata-mangler";
        public const string StringManglerName = "string-mangler";
        public const string CodeFlowManglerName = "code-flow-mangler";
        public const string IntManglerName = "int-mangler";
        public const string FeatureInjectorName = "feature-injector";
        public const string CFManglerJumpName = "code-flow-mangle-jump";
        public const string CFManglerSwitchName = "code-flow-mangle-switch";

        public const string EmbedderIndexName = "index";

        /// <summary>
        /// Start here - create new Netfuser context
        /// </summary>
        /// <returns>created context</returns>
        public static IContext NewContext() =>
            new ContextImpl();

    }
}