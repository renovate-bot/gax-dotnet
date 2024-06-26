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
using System.Threading.Tasks;

namespace Google.Api.Gax.Grpc
{
    internal static class ApiServerStreamingCall
    {
        internal static ApiServerStreamingCall<TRequest, TResponse> Create<TRequest, TResponse>(
            string methodName,
            Func<TRequest, CallOptions, AsyncServerStreamingCall<TResponse>> grpcCall,
            CallSettings baseCallSettings,
            IClock clock)
        {
            return new ApiServerStreamingCall<TRequest, TResponse>(
                methodName,
                (req, cs) => Task.FromResult(grpcCall(req, cs.ValidateNoRetry().ToCallOptions(clock))),
                (req, cs) => grpcCall(req, cs.ValidateNoRetry().ToCallOptions(clock)),
                baseCallSettings);
        }
    }

    /// <summary>
    /// Bridge between a server streaming RPC method and higher level
    /// abstractions, applying call settings as required.
    /// </summary>
    /// <typeparam name="TRequest">RPC request type</typeparam>
    /// <typeparam name="TResponse">RPC response type</typeparam>
    public sealed class ApiServerStreamingCall<TRequest, TResponse>
    {
        private readonly string _methodName;
        private readonly Func<TRequest, CallSettings, Task<AsyncServerStreamingCall<TResponse>>> _asyncCall;
        private readonly Func<TRequest, CallSettings, AsyncServerStreamingCall<TResponse>> _syncCall;

        internal ApiServerStreamingCall(
            string methodName,
            Func<TRequest, CallSettings, Task<AsyncServerStreamingCall<TResponse>>> asyncCall,
            Func<TRequest, CallSettings, AsyncServerStreamingCall<TResponse>> syncCall,
            CallSettings baseCallSettings)
        {
            _methodName = GaxPreconditions.CheckNotNull(methodName, nameof(methodName));
            _asyncCall = GaxPreconditions.CheckNotNull(asyncCall, nameof(asyncCall));
            _syncCall = GaxPreconditions.CheckNotNull(syncCall, nameof(syncCall));
            BaseCallSettings = baseCallSettings;
        }

        /// <summary>
        /// The base <see cref="CallSettings"/> for this API call; these can be further overridden by providing
        /// a <c>CallSettings</c> to <see cref="Call"/>.
        /// </summary>
        public CallSettings BaseCallSettings { get; }

        /// <summary>
        /// Initializes a streaming RPC call asynchronously.
        /// </summary>
        /// <param name="request">The RPC request.</param>
        /// <param name="perCallCallSettings">The call settings to apply to this specific call,
        /// overriding defaults where necessary.</param>
        /// <returns>A task representing the gRPC duplex streaming call object.</returns>
        public Task<AsyncServerStreamingCall<TResponse>> CallAsync(TRequest request, CallSettings perCallCallSettings) =>
            _asyncCall(request, BaseCallSettings.MergedWith(perCallCallSettings));

        /// <summary>
        /// Initializes a streaming RPC call.
        /// </summary>
        /// <param name="request">The RPC request.</param>
        /// <param name="perCallCallSettings">The call settings to apply to this specific call,
        /// overriding defaults where necessary.</param>
        /// <returns>A gRPC duplex streaming call object.</returns>
        public AsyncServerStreamingCall<TResponse> Call(TRequest request, CallSettings perCallCallSettings) =>
            _syncCall(request, BaseCallSettings.MergedWith(perCallCallSettings));

        /// <summary>
        /// Returns a new API call using the original base call settings merged with <paramref name="callSettings"/>.
        /// Where there's a conflict, the original base call settings have priority.
        /// </summary>
        internal ApiServerStreamingCall<TRequest, TResponse> WithMergedBaseCallSettings(CallSettings callSettings) =>
            new ApiServerStreamingCall<TRequest, TResponse>(_methodName, _asyncCall, _syncCall, callSettings.MergedWith(BaseCallSettings));

        /// <summary>
        /// Constructs a new <see cref="ApiServerStreamingCall{TRequest, TResponse}"/> that applies an overlay to
        /// the underlying <see cref="CallSettings"/>. If a value exists in both the original and
        /// the overlay, the overlay takes priority.
        /// </summary>
        /// <param name="callSettingsOverlayFn">Function that builds the overlay <see cref="CallSettings"/>.</param>
        /// <returns>A new <see cref="ApiServerStreamingCall{TRequest, TResponse}"/> with the overlay applied.</returns>
        public ApiServerStreamingCall<TRequest, TResponse> WithCallSettingsOverlay(Func<TRequest, CallSettings> callSettingsOverlayFn) =>
            new ApiServerStreamingCall<TRequest, TResponse>(
                _methodName,
                (req, cs) => _asyncCall(req, cs.MergedWith(callSettingsOverlayFn(req))),
                (req, cs) => _syncCall(req, cs.MergedWith(callSettingsOverlayFn(req))),
                BaseCallSettings);

        /// <summary>
        /// Constructs a new <see cref="ApiServerStreamingCall{TRequest, TResponse}"/> that applies an x-goog-request-params header to each request,
        /// using the specified parameter name and a value derived from the request.
        /// </summary>
        /// <remarks>Values produced by the function are URL-encoded; it is expected that <paramref name="parameterName"/> is already URL-encoded.</remarks>
        /// <param name="parameterName">The parameter name in the header. Must not be null.</param>
        /// <param name="valueSelector">A function to call on each request, to determine the value to specify in the header.
        /// The parameter must not be null, but may return null.</param>
        /// <returns>A new <see cref="ApiServerStreamingCall{TRequest, TResponse}"/> which applies the header on each request.</returns>
        public ApiServerStreamingCall<TRequest, TResponse> WithGoogleRequestParam(string parameterName, Func<TRequest, string> valueSelector)
        {
            GaxPreconditions.CheckNotNull(parameterName, nameof(parameterName));
            GaxPreconditions.CheckNotNull(valueSelector, nameof(valueSelector));
            return WithCallSettingsOverlay(request => CallSettings.FromGoogleRequestParamsHeader(parameterName, valueSelector(request)));
        }

        /// <summary>
        /// Constructs a new <see cref="ApiCall{TRequest, TResponse}"/> that applies an x-goog-request-params header to each request,
        /// using the <see cref="RoutingHeaderExtractor{TRequest}"/>.
        /// </summary>
        /// <remarks>Values produced by the function are URL-encoded.</remarks>
        /// <param name="extractor">The <see cref="RoutingHeaderExtractor{TRequest}"/> that extracts the value of the routing header from a request.</param>
        /// <returns>>A new <see cref="ApiCall{TRequest, TResponse}"/> which applies the header on each request.</returns>
        public ApiServerStreamingCall<TRequest, TResponse> WithExtractedGoogleRequestParam(RoutingHeaderExtractor<TRequest> extractor)
        {
            GaxPreconditions.CheckNotNull(extractor, nameof(extractor));

            return WithCallSettingsOverlay(request =>
            {
                var headerValue = extractor.ExtractHeader(request);

                return !string.IsNullOrWhiteSpace(headerValue)
                    ? CallSettings.FromGoogleRequestParamsHeader(headerValue)
                    : null; // CallSettings.Merge handles null correctly.
            });
        }

        internal ApiServerStreamingCall<TRequest, TResponse> WithLogging(ILogger logger) =>
            logger is null
                ? this
                : new ApiServerStreamingCall<TRequest, TResponse>(
                    _methodName,
                    _asyncCall.WithLogging(logger, _methodName),
                    _syncCall.WithLogging(logger, _methodName),
                    BaseCallSettings);

        internal ApiServerStreamingCall<TRequest, TResponse> WithTracing(ActivitySource activitySource) =>
            activitySource is null
                ? this
                : new ApiServerStreamingCall<TRequest, TResponse>(
                    _methodName,
                    _asyncCall.WithTracing(activitySource, _methodName),
                    _syncCall.WithTracing(activitySource, _methodName),
                    BaseCallSettings);
    }
}
