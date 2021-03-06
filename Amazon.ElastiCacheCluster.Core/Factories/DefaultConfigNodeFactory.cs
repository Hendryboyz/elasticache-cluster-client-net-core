﻿/*
 * Copyright 2014 Amazon.com, Inc. or its affiliates. All Rights Reserved.
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
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Amazon.ElastiCacheCluster.Core.Factories
{
    internal class DefaultConfigNodeFactory : IConfigNodeFactory
    {
        private readonly ILogger _logger;

        public DefaultConfigNodeFactory(ILogger logger)
        {
            _logger = logger;
        }

        public IMemcachedNode CreateNode(EndPoint endpoint, ISocketPoolConfiguration config)
        {
            return new MemcachedNode(endpoint, config, _logger);
        }
    }
}
