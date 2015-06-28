﻿using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;

// ReSharper disable StaticMemberInGenericType

namespace DD.Cloud.WebApi.TemplateToolkit
{
	using Utilities;

	/// <summary>
	///		Builds and submits HTTP requests for an <see cref="System.Net.Http.HttpClient"/>.
	/// </summary>
	/// <typeparam name="TContext">
	///		The type of object used by the request builder when resolving deferred template parameters.
	/// </typeparam>
	[DebuggerDisplay("{RequestUri}")]
	public class HttpRequestBuilder<TContext>
		: IHttpRequestBuilder<TContext>
	{
		#region Constants

		/// <summary>
		///		No request-configuration actions.
		/// </summary>
		internal static readonly ImmutableList<Action<HttpRequestMessage, TContext>>	NoConfigurationActions = ImmutableList<Action<HttpRequestMessage, TContext>>.Empty;

		/// <summary>
		///		No query parameters.
		/// </summary>
		static readonly ImmutableDictionary<string, Func<TContext, string>>				NoQueryParameters = ImmutableDictionary.Create<string, Func<TContext, string>>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		///		No template parameters.
		/// </summary>
		static readonly ImmutableDictionary<string, Func<TContext, string>>				NoTemplateParameters = ImmutableDictionary.Create<string, Func<TContext, string>>(StringComparer.OrdinalIgnoreCase);

		#endregion // Constants

		#region State

		/// <summary>
		///		The HTTP client to which requests will be submitted.
		/// </summary>
		readonly HttpClient													_httpClient;

		/// <summary>
		///		The request URI.
		/// </summary>
		readonly Uri														_requestUri;

		/// <summary>
		///		Is the request URI a template?
		/// </summary>
		readonly bool														_isTemplate;

		/// <summary>
		///		The URI query parameters (if any).
		/// </summary>
		readonly ImmutableDictionary<string, Func<TContext, string>>		_queryParameters = NoQueryParameters; 

		/// <summary>
		///		The URI template parameters (if any).
		/// </summary>
		readonly ImmutableDictionary<string, Func<TContext, string>>		_templateParameters = NoTemplateParameters; 

		/// <summary>
		///		Delegates that perform configuration of outgoing request messages.
		/// </summary>
		readonly ImmutableList<Action<HttpRequestMessage, TContext>>		_requestConfigurationActions;

		/// <summary>
		///		The media-type formatters to use.
		/// </summary>
		readonly ImmutableHashSet<MediaTypeFormatter>						_mediaTypeFormatters = HttpRequestBuilder.NoMediaTypeFormatters;

		/// <summary>
		///		The default object (if any) used by the request builder when resolving deferred template parameters.
		/// </summary>
		readonly TContext													_context;

		#endregion // State
		
		#region Construction

		/// <summary>
		///		Create a new HTTP request builder.
		/// </summary>
		/// <param name="httpClient">
		///		The HTTP client to which requests will be submitted.
		/// </param>
		/// <param name="requestUri">
		///		The request URI.
		/// </param>
		/// <param name="isTemplate">
		///		Does the URI represent a template?
		/// </param>
		internal HttpRequestBuilder(HttpClient httpClient, Uri requestUri, bool isTemplate = false)
		{
			if (requestUri == null)
				throw new ArgumentNullException("requestUri");

			_httpClient = httpClient;
			_requestUri = requestUri;
			_isTemplate = isTemplate;
			_requestConfigurationActions = NoConfigurationActions;
		}

		/// <summary>
		///		Create a new HTTP request builder from an existing HTTP request builder, but with the specified request URI template parameters.
		/// </summary>
		/// <param name="requestBuilder">
		///		The existing <see cref="HttpRequestBuilder{TContext}"/>.
		/// </param>
		/// <param name="requestUri">
		///		The new request URI.
		/// </param>
		/// <param name="requestUriTemplateParameters">
		///		Request template parameters.
		/// </param>
		protected HttpRequestBuilder(HttpRequestBuilder<TContext> requestBuilder, Uri requestUri, ImmutableDictionary<string, Func<TContext, string>> requestUriTemplateParameters)
			: this(requestBuilder, requestUri, isTemplate: true)
		{
			if (requestBuilder == null)
				throw new ArgumentNullException("requestBuilder");

			if (requestUri == null)
				throw new ArgumentNullException("requestUri");

			if (requestUriTemplateParameters == null)
				throw new ArgumentNullException("requestUriTemplateParameters");

			_templateParameters = requestUriTemplateParameters;
		}

		/// <summary>
		///		Create a new HTTP request builder from an existing HTTP request builder, but with the specified request URI query parameters.
		/// </summary>
		/// <param name="requestBuilder">
		///		The existing <see cref="HttpRequestBuilder{TContext}"/>.
		/// </param>
		/// <param name="queryParameters">
		///		The new query parameters.
		/// </param>
		protected HttpRequestBuilder(HttpRequestBuilder<TContext> requestBuilder, ImmutableDictionary<string, Func<TContext, string>> queryParameters)
			: this(requestBuilder)
		{
			if (requestBuilder == null)
				throw new ArgumentNullException("requestBuilder");

			if (queryParameters == null)
				throw new ArgumentNullException("queryParameters");

			_queryParameters = queryParameters;
		}

		/// <summary>
		///		Create a new HTTP request builder from an existing HTTP request builder, but with a different list of media-type formatters.
		/// </summary>
		/// <param name="requestBuilder">
		///		The existing <see cref="HttpRequestBuilder{TContext}"/>.
		/// </param>
		/// <param name="mediaTypeFormatters">
		///		The media-type formatters.
		/// </param>
		protected HttpRequestBuilder(HttpRequestBuilder<TContext> requestBuilder, ImmutableHashSet<MediaTypeFormatter> mediaTypeFormatters)
			: this(requestBuilder)
		{
			if (requestBuilder == null)
				throw new ArgumentNullException("requestBuilder");

			_mediaTypeFormatters = mediaTypeFormatters;
		}

		/// <summary>
		///		Create a new HTTP request builder from an existing HTTP request builder, but attached to a new <see cref="System.Net.Http.HttpClient"/>.
		/// </summary>
		/// <param name="requestBuilder">
		///		The existing <see cref="HttpRequestBuilder{TContext}"/>.
		/// </param>
		/// <param name="httpClient">
		///		The new <see cref="System.Net.Http.HttpClient"/>, or <c>null</c> if the new request builder should not be attached to a client.
		/// </param>
		protected HttpRequestBuilder(HttpRequestBuilder<TContext> requestBuilder, HttpClient httpClient)
			: this(requestBuilder)
		{
			_httpClient = httpClient;
		}

		/// <summary>
		///		Create a new HTTP request builder from an existing HTTP request builder, but attached to a new <see cref="System.Net.Http.HttpClient"/>.
		/// </summary>
		/// <param name="requestBuilder">
		///		The existing <see cref="HttpRequestBuilder{TContext}"/>.
		/// </param>
		/// <param name="context">
		///		The default object to be used by the request builder when resolving deferred template parameters.
		/// </param>
		protected HttpRequestBuilder(HttpRequestBuilder<TContext> requestBuilder, TContext context)
			: this(requestBuilder)
		{
			_context = context;
		}

		/// <summary>
		///		Create a new HTTP request builder from an existing HTTP request builder.
		/// </summary>
		/// <param name="requestBuilder">
		///		The existing <see cref="HttpRequestBuilder{TContext}"/>.
		/// </param>
		/// <remarks>
		///		Copy constructor.
		/// </remarks>
		protected HttpRequestBuilder(HttpRequestBuilder<TContext> requestBuilder)
		{
			if (requestBuilder == null)
				throw new ArgumentNullException("requestBuilder");

			_httpClient = requestBuilder._httpClient;
			_requestUri = requestBuilder._requestUri;
			_isTemplate = requestBuilder._isTemplate;
			_requestConfigurationActions = requestBuilder._requestConfigurationActions;
			_queryParameters = requestBuilder._queryParameters;
			_templateParameters = requestBuilder._templateParameters;
			_mediaTypeFormatters = requestBuilder._mediaTypeFormatters;
			_context = requestBuilder._context;
		}

		/// <summary>
		///		Create a new HTTP request builder from an existing HTTP request builder, but with a different request URI.
		/// </summary>
		/// <param name="requestBuilder">
		///		The existing <see cref="HttpRequestBuilder{TContext}"/>.
		/// </param>
		/// <param name="requestUri">
		///		The new request URI.
		/// </param>
		/// <param name="isTemplate">
		///		Does the URI represent a template?
		/// </param>
		protected HttpRequestBuilder(HttpRequestBuilder<TContext> requestBuilder, Uri requestUri, bool? isTemplate = null)
			: this(requestBuilder)
		{
			if (requestBuilder == null)
				throw new ArgumentNullException("requestBuilder");

			if (requestUri == null)
				throw new ArgumentNullException("requestUri");

			_requestUri = requestUri;
			_isTemplate = isTemplate ?? requestBuilder._isTemplate;
		}

		/// <summary>
		///		Create a new HTTP request builder from an existing HTTP request builder, but with additional configuration.
		/// </summary>
		/// <param name="requestBuilder">
		///		The existing <see cref="HttpRequestBuilder{TContext}"/>.
		/// </param>
		/// <param name="additionalRequestConfiguration">
		///		The additional request-configuration delegate.
		/// </param>
		protected HttpRequestBuilder(HttpRequestBuilder<TContext> requestBuilder, Action<HttpRequestMessage, TContext> additionalRequestConfiguration)
			: this(requestBuilder)
		{
			if (requestBuilder == null)
				throw new ArgumentNullException("requestBuilder");

			if (additionalRequestConfiguration == null)
				throw new ArgumentNullException("additionalRequestConfiguration");

			_requestConfigurationActions = requestBuilder._requestConfigurationActions.Add(additionalRequestConfiguration);
		}

		/// <summary>
		///		Create a new HTTP request builder from an existing HTTP request builder, but with additional configuration.
		/// </summary>
		/// <param name="requestBuilder">
		///		The existing <see cref="HttpRequestBuilder{TContext}"/>.
		/// </param>
		/// <param name="additionalRequestConfiguration">
		///		Additional request configuration delegates (if any).
		/// </param>
		protected HttpRequestBuilder(HttpRequestBuilder<TContext> requestBuilder, params Action<HttpRequestMessage, TContext>[] additionalRequestConfiguration)
			: this(requestBuilder)
		{
			if (requestBuilder == null)
				throw new ArgumentNullException("requestBuilder");

			if (additionalRequestConfiguration == null)
				throw new ArgumentNullException("additionalRequestConfiguration");

			_requestConfigurationActions = requestBuilder._requestConfigurationActions.AddRange(additionalRequestConfiguration);
		}

		/// <summary>
		///		Create a new HTTP request builder that is not attached to an <see cref="System.Net.Http.HttpClient"/>.
		/// </summary>
		/// <param name="requestUri">
		///		The request URI (can be relative or absolute).
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		[SuppressMessage("Microsoft.Design", "CA1057:StringUriOverloadsCallSystemUriOverloads", Justification = "False positive, see overload below.")]
		public static HttpRequestBuilder<TContext> Create(string requestUri)
		{
			if (String.IsNullOrWhiteSpace(requestUri))
				throw new ArgumentException("Argument cannot be null, empty, or composed entirely of whitespace: 'requestUri'.", "requestUri");

			return Create(
				new Uri(requestUri, UriKind.RelativeOrAbsolute)
			);
		}

		/// <summary>
		///		Create a new HTTP request builder that is not attached to an <see cref="System.Net.Http.HttpClient"/>.
		/// </summary>
		/// <param name="requestUri">
		///		The request URI (can be relative or absolute).
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public static HttpRequestBuilder<TContext> Create(Uri requestUri)
		{
			if (requestUri == null)
				throw new ArgumentNullException("requestUri");

			return new HttpRequestBuilder<TContext>(
				(HttpClient)null,
				requestUri
			);
		}

		#endregion // Construction

		#region Properties

		/// <summary>
		///		Is the request builder attached to an <see cref="System.Net.Http.HttpClient"/>?
		/// </summary>
		public bool IsAttachedToClient
		{
			get
			{
				return _httpClient != null;
			}
		}

		/// <summary>
		///		The HTTP client to which requests will be submitted.
		/// </summary>
		[CanBeNull]
		public HttpClient HttpClient
		{
			get
			{
				return _httpClient;
			}
		}

		/// <summary>
		///		The request URI.
		/// </summary>
		[NotNull]
		public Uri RequestUri
		{
			get
			{
				return _requestUri;
			}
		}

		/// <summary>
		///		Does the request builder have an absolute URI?
		/// </summary>
		public bool HasAbsoluteUri
		{
			get
			{
				return _requestUri.IsAbsoluteUri;
			}
		}

		/// <summary>
		///		Is the request URI a template?
		/// </summary>
		public bool IsTemplate
		{
			get
			{
				return _isTemplate;
			}
		}

		/// <summary>
		///		The URI template parameters (if any).
		/// </summary>
		[NotNull]
		public IReadOnlyDictionary<string, Func<TContext, string>> TemplateParameters
		{
			get
			{
				return _templateParameters;
			}
		}

		/// <summary>
		///		The media-type formatters to use.
		/// </summary>
		[NotNull]
		public IReadOnlyCollection<MediaTypeFormatter> MediaTypeFormatters
		{
			get
			{
				return _mediaTypeFormatters;
			}
		}

		/// <summary>
		///		The request builder's intrinsic context.
		/// </summary>
		/// <remarks>
		///		The intrinsic context is the default object (if any) used by the request builder when resolving deferred template parameters.
		///		If a different context is specified when building a request message, then that context will be used instead.
		/// 
		///		Warning - HTTP request builders that have intrinsic context are only thread-safe if their context is thread-safe.
		/// </remarks>
		public TContext Context
		{
			get
			{
				return _context;
			}
		}

		#endregion // Properties

		#region Invocation

		/// <summary>
		///		Ensure that the <see cref="HttpRequestBuilder{TContext}"/> has an <see cref="UriKind.Absolute">absolute</see> <see cref="Uri">URI</see>.
		/// </summary>
		/// <returns>
		///		The request builder's absolute URI.
		/// </returns>
		/// <exception cref="InvalidOperationException">
		///		The request builder has a <see cref="UriKind.Relative">relative</see> <see cref="Uri">URI</see> and is not attached to an <see cref="System.Net.Http.HttpClient"/> with an absolute <see cref="System.Net.Http.HttpClient.BaseAddress">base address</see>.
		/// </exception>
		[NotNull]
		public Uri EnsureAbsoluteUri()
		{
			Uri requestUri = _requestUri;
			if (requestUri.IsAbsoluteUri)
				return requestUri;

			// Try falling back to getting base address from attached HttpClient,
			if (IsAttachedToClient)
			{
				Uri baseUri = _httpClient.BaseAddress;
				if (baseUri != null && baseUri.IsAbsoluteUri)
					return new Uri(baseUri, requestUri);
			}
			
			throw new InvalidOperationException("The HTTP request builder does not have an absolute URI.");
		}

		/// <summary>
		///		Ensure that the <see cref="HttpRequestBuilder{TContext}"/> is attached to an <see cref="System.Net.Http.HttpClient"/>.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		///		The request builder is not attached to an HTTP client.
		/// </exception>
		public void EnsureAttachedToClient()
		{
			if (!IsAttachedToClient)
				throw new InvalidOperationException("The HTTP request builder is not attached to an HTTP client.");
		}

		/// <summary>
		///		Build and configure a new HTTP request message.
		/// </summary>
		/// <param name="httpMethod">
		///		The HTTP request method to use.
		/// </param>
		/// <param name="body">
		///		Optional <see cref="HttpContent"/> representing the request body.
		/// </param>
		/// <returns>
		///		The configured <see cref="HttpRequestMessage"/>.
		/// </returns>
		public HttpRequestMessage BuildRequestMessage(HttpMethod httpMethod, HttpContent body = null)
		{
			return BuildRequestMessage(httpMethod, default(TContext), body);
		}

		/// <summary>
		///		Build and configure a new HTTP request message.
		/// </summary>
		/// <param name="httpMethod">
		///		The HTTP request method to use.
		/// </param>
		/// <param name="context">
		///		The <typeparamref name="TContext"/> to use as the context for resolving any deferred template or query parameters.
		/// </param>
		/// <param name="body">
		///		Optional <see cref="HttpContent"/> representing the request body.
		/// </param>
		/// <returns>
		///		The configured <see cref="HttpRequestMessage"/>.
		/// </returns>
		public HttpRequestMessage BuildRequestMessage(HttpMethod httpMethod, TContext context, HttpContent body = null)
		{
			if (httpMethod == null)
				throw new ArgumentNullException("httpMethod");

			if (Equals(context, default(TContext)))
				context = _context;

			Uri requestUri = EnsureAbsoluteUri();
			if (_isTemplate)
			{
				Uri baseUri = new Uri(
					requestUri.GetComponents(
						UriComponents.Scheme | UriComponents.StrongAuthority,
						UriFormat.UriEscaped
					)
				);

				UriTemplate template = new UriTemplate(
					requestUri.GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped)
				);
				
				IDictionary<string, string> templateParameterValues = GetTemplateParameterValues(context);

				requestUri = template.Populate(baseUri, templateParameterValues);
			}
			
			// Merge in any other query parameters defined directly on the request builder.
			requestUri = MergeQueryParameters(requestUri, context);

			HttpRequestMessage requestMessage = null;
			try
			{
				requestMessage = new HttpRequestMessage(httpMethod, requestUri);
				if (body != null)
					requestMessage.Content = body;

				List<Exception> configurationActionExceptions = new List<Exception>();
				for (int configurationIndex = 0; configurationIndex < _requestConfigurationActions.Count; configurationIndex++)
				{
					Action<HttpRequestMessage, TContext> configurationAction = _requestConfigurationActions[configurationIndex];
					if (configurationAction == null)
						continue;

					try
					{
						configurationAction(requestMessage, context);
					}
					catch (Exception eConfigurationAction)
					{
						configurationActionExceptions.Add(eConfigurationAction);
					}
				}

				if (configurationActionExceptions.Count > 0)
				{
					throw new AggregateException(
						"One or more unhandled exceptions were encountered while configuring the outgoing request message.",
						configurationActionExceptions
					);
				}
			}
			catch
			{
				using (requestMessage)
				{
					throw;
				}
			}

			return requestMessage;
		}

		#endregion // Invocation

		#region Configuration

		/// <summary>
		///		Create a copy of the request builder with, but attached to the specified <see cref="System.Net.Http.HttpClient"/>.
		/// </summary>
		/// <param name="httpClient">
		///		The HTTP client.
		/// </param>
		/// <returns>
		///		A new <see cref="HttpRequestBuilder{TContext}"/>, or the existing <see cref="HttpRequestBuilder{TContext}"/> if it is already attached to the <see cref="System.Net.Http.HttpClient"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithClient(HttpClient httpClient)
		{
			if (httpClient == null)
				throw new ArgumentNullException("httpClient");

			if (_httpClient == httpClient)
				return this;

			return new HttpRequestBuilder<TContext>(this, httpClient);
		}

		/// <summary>
		///		Create a copy of the request builder with, but detached from its <see cref="System.Net.Http.HttpClient"/> (if any).
		/// </summary>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithoutClient()
		{
			if (_httpClient == null)
				return this;

			return new HttpRequestBuilder<TContext>(
				this,
				(HttpClient)null
			);
		}

		/// <summary>
		///		Create a copy of the request builder with the specified base URI.
		/// </summary>
		/// <param name="baseUri">
		///		The request base URI.
		/// 
		///		Must be an absolute URI.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		/// <exception cref="InvalidOperationException">
		///		The request builder already has an absolute URI.
		/// </exception>
		public HttpRequestBuilder<TContext> WithBaseUri(Uri baseUri)
		{
			if (baseUri == null)
				throw new ArgumentNullException("baseUri");

			if (!baseUri.IsAbsoluteUri)
				throw new ArgumentException("The supplied base URI is not an absolute URI.", "baseUri");

			if (HasAbsoluteUri)
				throw new InvalidOperationException("The request builder already has an absolute URI.");

			return new HttpRequestBuilder<TContext>(
				requestBuilder: this,
				requestUri: new Uri(baseUri, _requestUri)
			);
		}

		/// <summary>
		///		Create a copy of the request builder with the specified request URI.
		/// </summary>
		/// <param name="requestUri">
		///		The new request URI.
		/// 
		///		Must be an absolute URI (otherwise, use <see cref="WithRelativeRequestUri(System.Uri)"/>).
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithRequestUri(Uri requestUri)
		{
			if (requestUri == null)
				throw new ArgumentNullException("requestUri");

			if (!requestUri.IsAbsoluteUri)
				throw new ArgumentException("The specified URI is not an absolute URI.", "requestUri");

			return new HttpRequestBuilder<TContext>(this, requestUri);
		}

		/// <summary>
		///		Create a copy of the request builder with the specified request URI appended to its existing URI.
		/// </summary>
		/// <param name="relativeUri">
		///		The relative request URI.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		[SuppressMessage("Microsoft.Design", "CA1057:StringUriOverloadsCallSystemUriOverloads", Justification = "False positive; see overload below.")]
		public HttpRequestBuilder<TContext> WithRelativeRequestUri(string relativeUri)
		{
			if (String.IsNullOrWhiteSpace(relativeUri))
				throw new ArgumentException("Argument cannot be null, empty, or composed entirely of whitespace: 'relativeUri'.", "relativeUri");

			return WithRelativeRequestUri(
				new Uri(relativeUri, UriKind.Relative)
			);
		}

		/// <summary>
		///		Create a copy of the request builder with the specified request URI appended to its existing URI.
		/// </summary>
		/// <param name="relativeUri">
		///		The relative request URI.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithRelativeRequestUri(Uri relativeUri)
		{
			if (relativeUri == null)
				throw new ArgumentNullException("relativeUri");

			if (relativeUri.IsAbsoluteUri)
				throw new ArgumentException("The specified URI is not a relative URI.", "relativeUri");

			return new HttpRequestBuilder<TContext>(
				this,
				new Uri(_requestUri, relativeUri),
				isTemplate: _isTemplate
			);
		}

		/// <summary>
		///		Create a copy of the request builder with the specified request-configuration action.
		/// </summary>
		/// <param name="requestConfigurationAction">
		///		A delegate that configures outgoing request messages.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithRequestConfiguration(Action<HttpRequestMessage, TContext> requestConfigurationAction)
		{
			if (requestConfigurationAction == null)
				throw new ArgumentNullException("requestConfigurationAction");

			return new HttpRequestBuilder<TContext>(this, requestConfigurationAction);
		}

		/// <summary>
		///		Create a copy of the request builder with the specified request-configuration actions.
		/// </summary>
		/// <param name="requestConfigurationActions">
		///		A delegate that configures outgoing request messages.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithRequestConfiguration(params Action<HttpRequestMessage, TContext>[] requestConfigurationActions)
		{
			if (requestConfigurationActions == null)
				throw new ArgumentNullException("requestConfigurationActions");

			return new HttpRequestBuilder<TContext>(this, requestConfigurationActions);
		}
		
		/// <summary>
		///		Create a copy of the request builder with the specified request URI query parameter.
		/// </summary>
		/// <typeparam name="T">
		///		The parameter data-type.
		/// </typeparam>
		/// <param name="name">
		///		The parameter name.
		/// </param>
		/// <param name="getValue">
		///		Delegate that, given the current context, returns the parameter value (cannot be <c>null</c>).
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithQueryParameter<T>(string name, [NotNull] Func<TContext, T> getValue)
		{
			if (String.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Argument cannot be null, empty, or composed entirely of whitespace: 'name'.", "name");

			if (getValue == null)
				throw new ArgumentNullException("getValue");

			return new HttpRequestBuilder<TContext>(
				this,
				queryParameters: _queryParameters.SetItem(
					key: name,
					value: context =>
					{
						T value = getValue(context);

						return value != null ? value.ToString() : null;
					}
				)
			);
		}

		/// <summary>
		///		Create a copy of the request builder with the specified request URI query parameter.
		/// </summary>
		/// <typeparam name="T">
		///		The parameter data-type.
		/// </typeparam>
		/// <param name="name">
		///		The parameter name.
		/// </param>
		/// <param name="getValue">
		///		Delegate that returns the parameter value (cannot be <c>null</c>).
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithQueryParameter<T>(string name, [NotNull] Func<T> getValue)
		{
			if (String.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Argument cannot be null, empty, or composed entirely of whitespace: 'name'.", "name");

			if (getValue == null)
				throw new ArgumentNullException("getValue");

			return new HttpRequestBuilder<TContext>(
				this,
				queryParameters: _queryParameters.SetItem(
					key: name,
					value: context =>
					{
						T value = getValue();

						return value != null ? value.ToString() : null;
					}
				)
			);
		}

		/// <summary>
		///		Create a copy of the request builder with the specified request URI query parameter.
		/// </summary>
		/// <param name="queryParameters">
		///		A sequence of 0 or more key / value pairs representing the query parameters (values cannot be <c>null</c>).
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithQueryParameters(IEnumerable<KeyValuePair<string, Func<TContext, string>>> queryParameters)
		{
			if (queryParameters == null)
				throw new ArgumentNullException("queryParameters");

			bool modified = false;
			ImmutableDictionary<string, Func<TContext, string>>.Builder queryParametersBuilder = _queryParameters.ToBuilder();
			foreach (KeyValuePair<string, Func<TContext, string>> queryParameter in queryParameters)
			{
				if (queryParameter.Value == null)
				{
					throw new ArgumentException(
						String.Format(
							"Query parameter '{0}' has a null getter; this is not supported.",
							queryParameter.Key
						),
						"queryParameters"
					);
				}

				queryParametersBuilder[queryParameter.Key] = queryParameter.Value;
				modified = true;
			}

			if (!modified)
				return this;

			return new HttpRequestBuilder<TContext>(
				this,
				queryParameters: queryParametersBuilder.ToImmutable()
			);
		}

		/// <summary>
		///		Create a copy of the request builder without the specified request URI query parameter.
		/// </summary>
		/// <param name="name">
		///		The parameter name.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithoutQueryParameter(string name)
		{
			if (String.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Argument cannot be null, empty, or composed entirely of whitespace: 'name'.", "name");

			return new HttpRequestBuilder<TContext>(
				this,
				queryParameters: _queryParameters.Remove(name)
			);
		}

		/// <summary>
		///		Create a copy of the request builder without the specified request URI query parameters.
		/// </summary>
		/// <param name="names">
		///		The parameter names.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithoutQueryParameters(IEnumerable<string> names)
		{
			if (names == null)
				throw new ArgumentNullException("names");

			return new HttpRequestBuilder<TContext>(
				this,
				queryParameters: _queryParameters.RemoveRange(
					names.Where(
						name => !String.IsNullOrWhiteSpace(name)
					)
				)
			);
		}

		/// <summary>
		///		Create a copy of the request builder with the specified request URI template parameter.
		/// </summary>
		/// <typeparam name="T">
		///		The parameter data-type.
		/// </typeparam>
		/// <param name="name">
		///		The parameter name.
		/// </param>
		/// <param name="getValue">
		///		Delegate that, given the current context, returns the parameter value (cannot be <c>null</c>).
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithTemplateParameter<T>(string name, [NotNull] Func<TContext, T> getValue)
		{
			if (String.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Argument cannot be null, empty, or composed entirely of whitespace: 'name'.", "name");

			if (getValue == null)
				throw new ArgumentNullException("getValue");

			return new HttpRequestBuilder<TContext>(
				this,
				_requestUri,
				_templateParameters.SetItem(
					key: name,
					value: context =>
					{
						T value = getValue(context);

						return value != null ? value.ToString() : null;
					}
				)
			);
		}
		
		/// <summary>
		///		Create a copy of the request builder with the specified request URI template parameter.
		/// </summary>
		/// <typeparam name="T">
		///		The parameter data-type.
		/// </typeparam>
		/// <param name="name">
		///		The parameter name.
		/// </param>
		/// <param name="getValue">
		///		Delegate that returns the parameter value (cannot be <c>null</c>).
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithTemplateParameter<T>(string name, [NotNull] Func<T> getValue)
		{
			if (String.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Argument cannot be null, empty, or composed entirely of whitespace: 'name'.", "name");

			if (getValue == null)
				throw new ArgumentNullException("getValue");

			return new HttpRequestBuilder<TContext>(
				this,
				_requestUri,
				_templateParameters.SetItem(
					key: name,
					value: context =>
					{
						T value = getValue();
						
						return value != null ? value.ToString() : null;
					}
				)
			);
		}

		/// <summary>
		///		Create a copy of the request builder with the specified request URI template parameter.
		/// </summary>
		/// <param name="templateParameters">
		///		A sequence of 0 or more key / value pairs representing the template parameters (values cannot be <c>null</c>).
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithTemplateParameters(IEnumerable<KeyValuePair<string, Func<TContext, string>>> templateParameters)
		{
			if (templateParameters == null)
				throw new ArgumentNullException("templateParameters");

			bool modified = false;
			ImmutableDictionary<string, Func<TContext, string>>.Builder templateParametersBuilder = _templateParameters.ToBuilder();
			foreach (KeyValuePair<string, Func<TContext, string>> templateParameter in templateParameters)
			{
				if (templateParameter.Value == null)
				{
					throw new ArgumentException(
						String.Format(
							"Template parameter '{0}' has a null getter; this is not supported.",
							templateParameter.Key
						),
						"templateParameters"
					);
				}

				templateParametersBuilder[templateParameter.Key] = templateParameter.Value;
				modified = true;
			}

			if (!modified)
				return this;

			return new HttpRequestBuilder<TContext>(
				this,
				_requestUri,
				 templateParametersBuilder.ToImmutable()
			);
		}

		/// <summary>
		///		Create a copy of the request builder without the specified request URI template parameter.
		/// </summary>
		/// <param name="name">
		///		The parameter name.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithoutTemplateParameter(string name)
		{
			if (String.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Argument cannot be null, empty, or composed entirely of whitespace: 'name'.", "name");

			return new HttpRequestBuilder<TContext>(
				this,
				_requestUri,
				_templateParameters.Remove(name)
			);
		}

		/// <summary>
		///		Create a copy of the request builder without the specified request URI template parameters.
		/// </summary>
		/// <param name="names">
		///		The parameter names.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithoutTemplateParameters(IEnumerable<string> names)
		{
			if (names == null)
				throw new ArgumentNullException("names");

			return new HttpRequestBuilder<TContext>(
				this,
				_requestUri,
				_templateParameters.RemoveRange(
					names.Where(
						name => !String.IsNullOrWhiteSpace(name)
					)
				)
			);
		}

		/// <summary>
		///		Create a copy of the request builder, but with the specified media-type formatters.
		/// </summary>
		/// <param name="mediaTypeFormatters">
		///		The media-type formatters to use.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithMediaTypeFormatters(IEnumerable<MediaTypeFormatter> mediaTypeFormatters)
		{
			if (mediaTypeFormatters == null)
				throw new ArgumentNullException("mediaTypeFormatters");

			return new HttpRequestBuilder<TContext>(
				this,
				ImmutableHashSet.CreateRange(
					MediaTypeFormatterEqualityComparer.ByType,
					mediaTypeFormatters
				)
			);
		}

		/// <summary>
		///		Create a copy of the request builder, but with the specified media-type formatters appended to its existing list of media-type formatters.
		/// </summary>
		/// <param name="mediaTypeFormatters">
		///		The media-type formatters to use.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithAdditionalMediaTypeFormatters(IEnumerable<MediaTypeFormatter> mediaTypeFormatters)
		{
			if (mediaTypeFormatters == null)
				throw new ArgumentNullException("mediaTypeFormatters");

			if (_mediaTypeFormatters.IsSupersetOf(mediaTypeFormatters))
				return this;

			return new HttpRequestBuilder<TContext>(
				this,
				ImmutableHashSet
					.CreateRange(MediaTypeFormatterEqualityComparer.ByType, mediaTypeFormatters) // Ensure we overwrite existing formatters if they are supplied.
					.Union(_mediaTypeFormatters)
			);
		}

		/// <summary>
		///		Create a copy of the request builder, but without the specified media-type formatters.
		/// </summary>
		/// <param name="mediaTypeFormatters">
		///		The media-type formatters to remove.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithoutMediaTypeFormatters(IEnumerable<MediaTypeFormatter> mediaTypeFormatters)
		{
			if (mediaTypeFormatters == null)
				throw new ArgumentNullException("mediaTypeFormatters");

			if (_mediaTypeFormatters.IsSupersetOf(mediaTypeFormatters))
				return this;

			return new HttpRequestBuilder<TContext>(
				this,
				_mediaTypeFormatters.Except(mediaTypeFormatters)
			);
		}

		/// <summary>
		///		Create a copy of the request builder, but with the specified default context.
		/// </summary>
		/// <param name="context">
		///		The object to use, if one is not specified when building request messages, as the context for resolving template and query parameters.
		/// </param>
		/// <returns>
		///		The new <see cref="HttpRequestBuilder{TContext}"/>.
		/// </returns>
		public HttpRequestBuilder<TContext> WithContext(TContext context)
		{
			if (Equals(_context, context))
				return this;

			return new HttpRequestBuilder<TContext>(this, context);
		}

		#endregion // Configuration

		#region Helpers

		/// <summary>
		///		Merge the request builder's query parameters (if any) into the request URI.
		/// </summary>
		/// <param name="requestUri">
		///		The request URI.
		/// </param>
		/// <param name="context">
		///		The current context.
		/// </param>
		/// <returns>
		///		The request URI with query parameters merged into it.
		/// </returns>
		Uri MergeQueryParameters(Uri requestUri, TContext context)
		{
			if (requestUri == null)
				throw new ArgumentNullException("requestUri");

			if (_queryParameters.Count == 0)
				return requestUri;

			NameValueCollection queryParameters = requestUri.ParseQueryString();
			foreach (KeyValuePair<string, Func<TContext, string>> queryParameter in _queryParameters)
			{
				string queryParameterValue = queryParameter.Value(context);
				if (queryParameterValue != null)
					queryParameters[queryParameter.Key] = queryParameterValue;
				else
					queryParameters.Remove(queryParameter.Key);
			}

			return requestUri.WithQueryParameters(queryParameters);
		}

		/// <summary>
		///		Get a dictionary mapping template parameters (if any) to their current values.
		/// </summary>
		/// <param name="context">
		///		The current context.
		/// </param>
		/// <returns>
		///		A dictionary of key / value pairs (any parameters whose value-getters return null will be omitted).
		/// </returns>
		IDictionary<string, string> GetTemplateParameterValues(TContext context)
		{
			return
				_templateParameters.Select(
					templateParameter =>
					{
						Debug.Assert(templateParameter.Value != null);

						return new
						{
							templateParameter.Key,
							Value = templateParameter.Value(context)
						};
					}
				)
				.Where(
					templateParameter => templateParameter.Value != null
				)
				.ToDictionary(
					templateParameter => templateParameter.Key,
					templateParameter => templateParameter.Value
				);
		}

		#endregion // Helpers
	}
}
