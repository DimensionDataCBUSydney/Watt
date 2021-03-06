﻿using System;
using System.Collections.Specialized;
using System.Text;

namespace DD.Cloud.WebApi.TemplateToolkit.Utilities
{
	/// <summary>
	///		Helper methods for working with <see cref="Uri"/>s.
	/// </summary>
	public static class UriHelper
	{
		/// <summary>
		///		Create a copy of URI with its <see cref="Uri.Query">query</see> component populated with the supplied parameters.
		/// </summary>
		/// <param name="uri">
		///		The <see cref="Uri"/> used to construct the URI.
		/// </param>
		/// <param name="parameters">
		///		A <see cref="NameValueCollection"/> representing the query parameters.
		/// </param>
		/// <returns>
		///		A new URI with the specified query.
		/// </returns>
		public static Uri WithQueryParameters(this Uri uri, NameValueCollection parameters)
		{
			if (uri == null)
				throw new ArgumentNullException("uri");

			if (parameters == null)
				throw new ArgumentNullException("parameters");

			return
				new UriBuilder(uri)
					.WithQueryParameters(parameters)
					.Uri;
		}

		/// <summary>
		///		Populate the <see cref="UriBuilder.Query">query</see> component of the URI.
		/// </summary>
		/// <param name="uriBuilder">
		///		The <see cref="UriBuilder"/> used to construct the URI
		/// </param>
		/// <param name="parameters">
		///		A <see cref="NameValueCollection"/> representing the query parameters.
		/// </param>
		/// <returns>
		///		The <paramref name="uriBuilder">URI builder</paramref> (enables inline use).
		/// </returns>
		public static UriBuilder WithQueryParameters(this UriBuilder uriBuilder, NameValueCollection parameters)
		{
			if (uriBuilder == null)
				throw new ArgumentNullException("uriBuilder");

			if (parameters == null)
				throw new ArgumentNullException("parameters");

			if (parameters.Count == 0)
				return uriBuilder;

			// Yes, you could do this using String.Join, but it seems a bit wasteful to allocate all those "key=value" strings only to throw them away again.

			Action<StringBuilder, int> addQueryParameter = (builder, parameterIndex) =>
			{
				string parameterName = parameters.GetKey(parameterIndex);
				string parameterValue = parameters.Get(parameterIndex);

				builder.Append(parameterName);
				builder.Append('=');
				builder.Append(
					Uri.EscapeUriString(parameterValue)
				);
			};

			StringBuilder queryBuilder = new StringBuilder();

			// First parameter has no prefix.
			addQueryParameter(queryBuilder, 0);

			// Subsequent parameters are separated with an '&'
			for (int parameterIndex = 1; parameterIndex < parameters.Count; parameterIndex++)
			{
				queryBuilder.Append('&');
				addQueryParameter(queryBuilder, parameterIndex);
			}

			uriBuilder.Query = queryBuilder.ToString();

			return uriBuilder;
		}

		/// <summary>
		///		Append a relative URI to the base URI.
		/// </summary>
		/// <param name="baseUri">
		///		The base URI.
		/// 
		///		A trailing "/" will be appended, if necessary.
		/// </param>
		/// <param name="relativeUri">
		///		The relative URI to append (leading slash will be trimmed, if required).
		/// </param>
		/// <returns>
		///		The concatenated URI.
		/// </returns>
		/// <remarks>
		///		This function is required because, sometimes, appending of a relative path to a URI can behave counter-intuitively.
		///		If the base URI does not have a trailing "/", then its last path segment is *replaced* by the relative UI. This is hardly ever what you actually want.
		/// </remarks>
		public static Uri AppendRelativeUri(this Uri baseUri, Uri relativeUri)
		{
			if (baseUri == null)
				throw new ArgumentNullException(nameof(baseUri));

			if (relativeUri == null)
				throw new ArgumentNullException(nameof(relativeUri));

			if (relativeUri.IsAbsoluteUri)
				return relativeUri;

			if (!baseUri.IsAbsoluteUri)
				return new Uri(AppendPaths(baseUri.ToString(), relativeUri.ToString()), UriKind.Relative);

			UriBuilder uriBuilder = new UriBuilder(baseUri);

			string[] relativePathAndQuery =
				relativeUri.ToString().Split(
					new char[1] { '?' },
					count: 2,
					options: StringSplitOptions.RemoveEmptyEntries
				);

			uriBuilder.Path = AppendPaths(uriBuilder.Path, relativePathAndQuery[0]);

			if (relativePathAndQuery.Length == 2)
				uriBuilder.Query = relativePathAndQuery[1];

			return uriBuilder.Uri;
		}

		/// <summary>
		///		Contatenate 2 relative URI paths.
		/// </summary>
		/// <param name="basePath">
		///		The base URI path.
		/// </param>
		/// <param name="relativePath">
		///		The relative URI path to append to the base URI path.
		/// </param>
		/// <returns>
		///		The appended paths, separated by a single slash.
		/// </returns>
		static string AppendPaths(string basePath, string relativePath)
		{
			if (basePath == null)
				throw new ArgumentNullException(nameof(basePath));

			if (relativePath == null)
				throw new ArgumentNullException(nameof(relativePath));

			StringBuilder pathBuilder = new StringBuilder(basePath);

			if (pathBuilder.Length == 0 || (int)pathBuilder[pathBuilder.Length - 1] != 47)
				pathBuilder.Append("/");

			int relativePathStartIndex =
					(relativePath.Length <= 0 || (int)relativePath[0] != 47) ? 0 : 1;

			pathBuilder.Append(
				relativePath, 
				startIndex: (relativePath.Length <= 0 || relativePath[0] != 47) ? 0 : 1,
				count: relativePath.Length - relativePathStartIndex
				);

			return pathBuilder.ToString();
		}
	}
}
