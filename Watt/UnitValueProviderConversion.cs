using System;
using System.Diagnostics.CodeAnalysis;

namespace DD.Cloud.WebApi.TemplateToolkit
{
	/// <summary>
	///		Conversion operations for a value provider that targets an empty (<see cref="Unit"/>) context.
	/// </summary>
	/// <typeparam name="TValue">
	///		The type of value returned by the value provider.
	/// </typeparam>
	/// <remarks>
	///		Since <see cref="Unit"/> is essentially equivalent to <c>void</c>, this conversion removes the requirement for the target context type to be a more-derived class.
	///		This enables base requests to be defined without a context type, and then more-derived requests can be specialised for their specific context types.
	/// 
	///		Still need to implement an extension method equivalent to <see cref="HttpRequestBuilder{TContext}.WithDerivedContext{TDerivedContext}"/> for a request builder that targets <see cref="Unit"/> as its context type.
	/// </remarks>
	[SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "Intermediate type used only to guide intellisense.")]
	public struct UnitValueProviderConversion<TValue>
	{
		/// <summary>
		///		Create a new value-provider conversion.
		/// </summary>
		/// <param name="valueProvider">
		///		The value provider being converted.
		/// </param>
		public UnitValueProviderConversion(IValueProvider<Unit, TValue> valueProvider)
			: this()
		{
			if (valueProvider == null)
				throw new ArgumentNullException("valueProvider");

			ValueProvider = valueProvider;
		}

		/// <summary>
		///		The value provider being converted.
		/// </summary>
		public IValueProvider<Unit, TValue> ValueProvider
		{
			get;
			private set;
		}

		/// <summary>
		///		Wrap the specified value provider in a value provider that utilises a specific context type.
		/// </summary>
		/// <typeparam name="TContext">
		///		The type used by the new provider as a context for each request.
		/// </typeparam>
		/// <returns>
		///		The outer (converting) value provider.
		/// </returns>
		public IValueProvider<TContext, TValue> ContextTo<TContext>()
		{
			// Can't close over members of structs.
			IValueProvider<Unit, TValue> valueProvider = ValueProvider;

			return ValueProvider<TContext>.FromSelector(
				context => valueProvider.Get(Unit.Value)
			);
		}

		/// <summary>
		///		Wrap the value provider in a value provider that converts its value to a string.
		/// </summary>
		/// <returns>
		///		The outer (converting) value provider.
		/// </returns>
		/// <remarks>
		///		If the underlying value is <c>null</c> then the converted string value will be <c>null</c>, too.
		/// </remarks>
		public IValueProvider<Unit, string> ValueToString()
		{
			// Special-case conversion to save on allocations.
			if (typeof(TValue) == typeof(string))
				return (IValueProvider<Unit, string>)ValueProvider;

			// Can't close over members of structs.
			IValueProvider<Unit, TValue> valueProvider = ValueProvider;

			return ValueProvider<Unit>.FromSelector(
				context =>
				{
					TValue value = valueProvider.Get(Unit.Value);

					return value != null ? value.ToString() : null;
				}
			);
		}
	}
}