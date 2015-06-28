﻿using System;

namespace DD.Cloud.WebApi.TemplateToolkit.Templates
{
	/// <summary>
	///		The base class for template segments that represent components of a URI's query.
	/// </summary>
	abstract class QuerySegment
		: TemplateSegment
	{
		/// <summary>
		///		The name of the query parameter that the segment represents.
		/// </summary>
		readonly string _queryParameterName;

		/// <summary>
		///		Create a new query segment.
		/// </summary>
		/// <param name="queryParameterName">
		///		The name of the query parameter that the segment represents.
		/// </param>
		protected QuerySegment(string queryParameterName)
		{
			if (String.IsNullOrWhiteSpace(queryParameterName))
				throw new ArgumentException("Argument cannot be null, empty, or composed entirely of whitespace: 'queryParameterName'.", "queryParameterName");

			_queryParameterName = queryParameterName;
		}

		/// <summary>
		///		The name of the query parameter that the segment represents.
		/// </summary>
		public string QueryParameterName
		{
			get
			{
				return _queryParameterName;
			}
		}
	}
}
