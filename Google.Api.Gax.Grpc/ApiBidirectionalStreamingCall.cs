﻿/*
 * Copyright 2016 Google Inc. All Rights Reserved.
 * Use of this source code is governed by a BSD-style
 * license that can be found in the LICENSE file or at
 * https://developers.google.com/open-source/licenses/bsd
 */

using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace Google.Api.Gax.Grpc
{
    internal static class ApiBidirectionalStreamingCall
    {
        internal static ApiBidirectionalStreamingCall<TRequest, TResponse> Create<TRequest, TResponse>(
            string methodName,
            Func<CallOptions, AsyncDuplexStreamingCall<TRequest, TResponse>> grpcCall,
            CallSettings baseCallSettings,
            BidirectionalStreamingSettings streamingSettings,
            IClock clock)
        {
            return new ApiBidirectionalStreamingCall<TRequest, TResponse>(
                methodName,
                cs => grpcCall(cs.ValidateNoRetry().ToCallOptions(clock)),
                baseCallSettings,
                streamingSettings);
        }
    }

    /// <summary>
    /// Bridge between a duplex streaming RPC method and higher level
    /// abstractions, applying call settings as required.
    /// </summary>
    /// <typeparam name="TRequest">RPC request type</typeparam>
    /// <typeparam name="TResponse">RPC response type</typeparam>
    public sealed class ApiBidirectionalStreamingCall<TRequest, TResponse>
    {
        private readonly string _methodName;
        private readonly Func<CallSettings, AsyncDuplexStreamingCall<TRequest, TResponse>> _call;

        internal ApiBidirectionalStreamingCall(
            string methodName,
            Func<CallSettings, AsyncDuplexStreamingCall<TRequest, TResponse>> call,
            CallSettings baseCallSettings,
            BidirectionalStreamingSettings streamingSettings)
        {
            _methodName = GaxPreconditions.CheckNotNull(methodName, nameof(methodName));
            _call = GaxPreconditions.CheckNotNull(call, nameof(call));
            BaseCallSettings = baseCallSettings;
            StreamingSettings = streamingSettings;
        }

        /// <summary>
        /// The base <see cref="CallSettings"/> for this API call; these can be further overridden by providing
        /// a <c>CallSettings</c> to <see cref="Call"/>.
        /// </summary>
        public CallSettings BaseCallSettings { get; }

        /// <summary>
        /// Streaming settings.
        /// </summary>
        public BidirectionalStreamingSettings StreamingSettings { get; }

        /// <summary>
        /// Initializes a streaming RPC call.
        /// </summary>
        /// <param name="perCallCallSettings">The call settings to apply to this specific call,
        /// overriding defaults where necessary.</param>
        /// <returns>A gRPC duplex streaming call object.</returns>
        public AsyncDuplexStreamingCall<TRequest, TResponse> Call(CallSettings perCallCallSettings) =>
            _call(BaseCallSettings.MergedWith(perCallCallSettings).ValidateNoRetry());

        /// <summary>
        /// Returns a new API call using the original base call settings merged with <paramref name="callSettings"/>.
        /// Where there's a conflict, the original base call settings have priority.
        /// </summary>
        internal ApiBidirectionalStreamingCall<TRequest, TResponse> WithMergedBaseCallSettings(CallSettings callSettings) =>
            new ApiBidirectionalStreamingCall<TRequest, TResponse>(_methodName, _call, callSettings.MergedWith(BaseCallSettings), StreamingSettings);

        internal ApiBidirectionalStreamingCall<TRequest, TResponse> WithLogging(ILogger logger) =>
            logger is null
                ? this
                : new ApiBidirectionalStreamingCall<TRequest, TResponse>(_methodName, _call.WithLogging(logger, _methodName), BaseCallSettings, StreamingSettings);

        internal ApiBidirectionalStreamingCall<TRequest, TResponse> WithTracing(ActivitySource activitySource) =>
            activitySource is null
                ? this
                : new ApiBidirectionalStreamingCall<TRequest, TResponse>(_methodName, _call.WithTracing(activitySource, _methodName), BaseCallSettings, StreamingSettings);
    }
}
