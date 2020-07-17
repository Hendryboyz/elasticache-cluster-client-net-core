using Enyim.Caching;
using Microsoft.Extensions.Logging;

namespace Amazon.ElastiCacheCluster.Core
{
    /// <summary>
    /// Used to instantiate MemcachedClients with auto discovery enabled.
    /// Only use these for easy creation because the ability to get information from the config object is lost
    /// </summary>
    public static class ClusterClient
    {
        /// <summary>
        /// Creates a MemcachedClient using the settings found in the app.config section "clusterclient"
        /// </summary>
        /// <returns>A new MemcachedClient configured for auto discovery</returns>
        public static MemcachedClient CreateClient(ILoggerFactory loggerFactory)
        {
            return new MemcachedClient(loggerFactory, new ElastiCacheClusterConfig(CreateLogger(loggerFactory)));
        }

        private static ILogger CreateLogger(ILoggerFactory loggerFactory)
        {
            return loggerFactory.CreateLogger(typeof(ClusterClient));
        }

        /// <summary>
        /// Creates a MemcachedClient using the settings found in the app.config section specified
        /// </summary>
        /// <param name="section">A section in app.config that has a endpoint field</param>
        /// <returns>A new MemcachedClient configured for auto discovery</returns>
        public static MemcachedClient CreateClient(ILoggerFactory loggerFactory, string section)
        {
            return new MemcachedClient(loggerFactory, 
                new ElastiCacheClusterConfig(section, CreateLogger(loggerFactory)));
        }

        /// <summary>
        /// Creates a MemcachedClient using the default settings with the endpoint and port specified
        /// </summary>
        /// <param name="endpoint">The url for the cluster endpoint containing .cfg.</param>
        /// <param name="port">The port to access the cluster on</param>
        /// <returns>A new MemcachedClient configured for auto discovery</returns>
        public static MemcachedClient CreateClient(ILoggerFactory loggerFactory, string endpoint, int port)
        {
            ILogger logger = loggerFactory.CreateLogger(typeof(ClusterClient));
            return new MemcachedClient(loggerFactory, new ElastiCacheClusterConfig(endpoint, port, CreateLogger(loggerFactory)));
        }

        /// <summary>
        /// Creates a MemcachedClient using the Client config provided
        /// </summary>
        /// <param name="config">The config to instantiate the client with</param>
        /// <returns>A new MemcachedClient configured for auto discovery</returns>
        public static MemcachedClient CreateClient(ILoggerFactory loggerFactory, ElastiCacheClusterConfig config)
        {
            return new MemcachedClient(loggerFactory, config);
        }
    }
}
