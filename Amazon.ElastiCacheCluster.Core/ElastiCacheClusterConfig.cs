﻿/*
 * Copyright 2014 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * 
 * Portions copyright 2010 Attila Kiskó, enyim.com. Please see LICENSE.txt
 * for applicable license terms and NOTICE.txt for applicable notices.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 * 
 *  http://aws.amazon.com/apache2.0
 * 
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Net;
using Enyim.Caching.Memcached;
using Enyim.Reflection;
using Enyim.Caching.Configuration;
using Amazon.ElastiCacheCluster.Core;
using Amazon.ElastiCacheCluster.Core.Pools;
using Amazon.ElastiCacheCluster.Core.Factories;
using System.Configuration;
using Microsoft.Extensions.Logging;

namespace Amazon.ElastiCacheCluster
{
    /// <summary>
    /// Configuration class for auto discovery
    /// </summary>
    public class ElastiCacheClusterConfig : IMemcachedClientConfiguration
    {
        // these are lazy initialized in the getters
        private Type nodeLocator;
        private ITranscoder transcoder;
        private IMemcachedKeyTransformer keyTransformer;

        internal ClusterConfigSettings setup;
        internal AutoServerPool Pool;
        internal IConfigNodeFactory nodeFactory;
        internal ILogger logger;

        /// <summary>
        /// The node used to check the cluster's configuration
        /// </summary>
        public DiscoveryNode DiscoveryNode { get; private set; }

        #region Constructors

        /// <summary>
        /// Initializes a MemcahcedClient config with auto discovery enabled from the app.config clusterclient section
        /// </summary>
        public ElastiCacheClusterConfig(ILogger logger)
            : this(null as ClusterConfigSettings, logger) { }

        /// <summary>
        /// Initializes a MemcahcedClient config with auto discovery enabled from the app.config with the specified section
        /// </summary>
        /// <param name="section">The section to get config settings from</param>
        public ElastiCacheClusterConfig(string section, ILogger logger)
            : this(ConfigurationManager.GetSection(section) as ClusterConfigSettings, logger) { }

        /// <summary>
        /// Initializes a MemcahcedClient config with auto discovery enabled
        /// </summary>
        /// <param name="hostname">The hostname of the cluster containing ".cfg."</param>
        /// <param name="port">The port to connect to for communication</param>
        public ElastiCacheClusterConfig(string hostname, int port, ILogger logger)
            : this(new ClusterConfigSettings(hostname, port), logger) { }

        /// <summary>
        /// Initializes a MemcahcedClient config with auto discovery enabled using the setup provided
        /// </summary>
        /// <param name="setup">The setup to get conifg settings from</param>
        public ElastiCacheClusterConfig(ClusterConfigSettings setup, ILogger logger)
        {
            if (setup == null)
            {
                try
                {
                    setup = ConfigurationManager.GetSection("clusterclient") as ClusterConfigSettings;
                    if (setup == null)
                    {
                        throw new ConfigurationErrorsException("Could not instantiate from app.config, setup was null");
                    }
                }
                catch (Exception ex)
                {
                    throw new ConfigurationErrorsException("Could not instantiate from app.config\n" + ex.Message);
                }
            }

            if (setup.ClusterEndPoint == null)
                throw new ArgumentException("Cluster Settings are null");
            if (String.IsNullOrEmpty(setup.ClusterEndPoint.HostName))
                throw new ArgumentNullException("hostname");
            if (setup.ClusterEndPoint.Port <= 0)
                throw new ArgumentException("Port cannot be 0 or less");

            this.logger = logger;
            this.setup = setup;
            this.Servers = new List<EndPoint>();

            this.Protocol = setup.Protocol;

            if (setup.KeyTransformer == null)
                this.KeyTransformer = new DefaultKeyTransformer();
            else
                this.KeyTransformer = setup.KeyTransformer.Create() ?? new DefaultKeyTransformer();

            this.SocketPool = (ISocketPoolConfiguration)setup.SocketPool ?? new SocketPoolConfiguration();
            this.Authentication = (IAuthenticationConfiguration)setup.Authentication ?? new AuthenticationConfiguration();

            this.nodeFactory = setup.NodeFactory ?? new DefaultConfigNodeFactory(logger);
            this.NodeLocatorFactory = setup.NodeLocator;
            this.nodeLocator = typeof(DefaultNodeLocator);

            if (setup.Transcoder != null)
            {
                this.transcoder = setup.Transcoder.Create() ?? new DefaultTranscoder();
            }

            if (setup.ClusterEndPoint.HostName.IndexOf(".cfg", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (setup.ClusterNode != null)
                {
                    var _tries = setup.ClusterNode.NodeTries > 0 ? setup.ClusterNode.NodeTries : DiscoveryNode.DEFAULT_TRY_COUNT;
                    var _delay = setup.ClusterNode.NodeDelay >= 0 ? setup.ClusterNode.NodeDelay : DiscoveryNode.DEFAULT_TRY_DELAY;
                    this.DiscoveryNode = new DiscoveryNode(this, setup.ClusterEndPoint.HostName, setup.ClusterEndPoint.Port, _tries, _delay);
                }
                else
                    this.DiscoveryNode = new DiscoveryNode(this, setup.ClusterEndPoint.HostName, setup.ClusterEndPoint.Port);
            }
            else
            {
                throw new ArgumentException("The provided endpoint does not support auto discovery");
            }
        }

        #endregion

        #region Members

        /// <summary>
        /// Gets a list of <see cref="T:IPEndPoint"/> each representing a Memcached server in the pool.
        /// </summary>
        public IList<EndPoint> Servers { get; }

        /// <summary>
        /// Gets the configuration of the socket pool.
        /// </summary>
        public ISocketPoolConfiguration SocketPool { get; private set; }

        /// <summary>
        /// Gets the authentication settings.
        /// </summary>
        public IAuthenticationConfiguration Authentication { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="T:Enyim.Caching.Memcached.IMemcachedKeyTransformer"/> which will be used to convert item keys for Memcached.
        /// </summary>
        public IMemcachedKeyTransformer KeyTransformer
        {
            get { return this.keyTransformer ?? (this.keyTransformer = new DefaultKeyTransformer()); }
            set { this.keyTransformer = value; }
        }

        /// <summary>
        /// Gets or sets the Type of the <see cref="T:Enyim.Caching.Memcached.IMemcachedNodeLocator"/> which will be used to assign items to Memcached nodes.
        /// </summary>
        /// <remarks>If both <see cref="M:NodeLocator"/> and  <see cref="M:NodeLocatorFactory"/> are assigned then the latter takes precedence.</remarks>
        public Type NodeLocator
        {
            get { return this.nodeLocator; }
            set
            {
                ConfigurationHelper.CheckForInterface(value, typeof(IMemcachedNodeLocator));
                this.nodeLocator = value;
            }
        }

        /// <summary>
        /// Gets or sets the NodeLocatorFactory instance which will be used to create a new IMemcachedNodeLocator instances.
        /// </summary>
        /// <remarks>If both <see cref="M:NodeLocator"/> and  <see cref="M:NodeLocatorFactory"/> are assigned then the latter takes precedence.</remarks>
        public IProviderFactory<IMemcachedNodeLocator> NodeLocatorFactory { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="T:Enyim.Caching.Memcached.ITranscoder"/> which will be used serialize or deserialize items.
        /// </summary>
        public ITranscoder Transcoder
        {
            get { return this.transcoder ?? (this.transcoder = new DefaultTranscoder()); }
            set { this.transcoder = value; }
        }


        /// <summary>
        /// Gets or sets the type of the communication between client and server.
        /// </summary>
        public MemcachedProtocol Protocol { get; set; }

        #endregion

        #region [ interface ]
        IList<EndPoint> IMemcachedClientConfiguration.Servers
        {
            get { return this.Servers; }
        }

        ISocketPoolConfiguration IMemcachedClientConfiguration.SocketPool
        {
            get { return this.SocketPool; }
        }

        IAuthenticationConfiguration IMemcachedClientConfiguration.Authentication
        {
            get { return this.Authentication; }
        }

        IMemcachedKeyTransformer IMemcachedClientConfiguration.CreateKeyTransformer()
        {
            return this.KeyTransformer;
        }

        IMemcachedNodeLocator IMemcachedClientConfiguration.CreateNodeLocator()
        {
            var f = this.NodeLocatorFactory;
            if (f != null) return f.Create();

            return this.NodeLocator == null
                    ? new DefaultNodeLocator()
                    : (IMemcachedNodeLocator)FastActivator.Create(this.NodeLocator);
        }

        ITranscoder IMemcachedClientConfiguration.CreateTranscoder()
        {
            return this.Transcoder;
        }

        IServerPool IMemcachedClientConfiguration.CreatePool()
        {
            switch (this.Protocol)
            {
                case MemcachedProtocol.Text:
                    this.Pool = new AutoServerPool(this, new Enyim.Caching.Memcached.Protocol.Text.TextOperationFactory(), logger);
                    break;
                case MemcachedProtocol.Binary:
                    this.Pool = new AutoBinaryPool(this, logger);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unknown protocol: " + (int)this.Protocol);
            }

            return this.Pool;
        }
        #endregion
    }
}
