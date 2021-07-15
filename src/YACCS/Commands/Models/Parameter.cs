﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using YACCS.Commands.Attributes;
using YACCS.Commands.Linq;
using YACCS.NamedArguments;
using YACCS.Preconditions;
using YACCS.TypeReaders;

namespace YACCS.Commands.Models
{
	[DebuggerDisplay(CommandServiceUtils.DEBUGGER_DISPLAY)]
	public sealed class Parameter : EntityBase, IParameter
	{
		private static readonly object NoDefaultValue = new();

		private ITypeReader? _OverriddenTypeReader;

		public object? DefaultValue { get; set; } = NoDefaultValue;
		public bool HasDefaultValue
		{
			get => DefaultValue != NoDefaultValue;
			set
			{
				if (!value)
				{
					DefaultValue = NoDefaultValue;
				}
			}
		}
		public string OriginalParameterName { get; }
		public Type ParameterType { get; }
		public ITypeReader? TypeReader
		{
			get => _OverriddenTypeReader;
			set
			{
				value?.ThrowIfInvalidTypeReader(ParameterType);
				_OverriddenTypeReader = value;
			}
		}
		private string DebuggerDisplay => this.FormatForDebuggerDisplay();

		public Parameter(Type type, string name, ICustomAttributeProvider? provider)
			: base(provider)
		{
			if (type == typeof(void))
			{
				throw new ArgumentException($"'{name}' cannot have a parameter type of void.", nameof(type));
			}

			OriginalParameterName = name;
			ParameterType = type;

			// After everything else has been set, some attributes may add additional stuff
			// like the named arguments attribute on the parameter's type will add
			// the named arguments parameter precondition which verifies that every
			// property has been set or has a default value
			// Call provider.GetCustomAttributes again because Attributes may be modified
			IEnumerable<object> attrs = type.GetCustomAttributes(true);
			if (provider != null)
			{
				attrs = provider.GetCustomAttributes(true).Concat(attrs);
			}
			foreach (var modifier in attrs.OfType<IParameterModifierAttribute>())
			{
				modifier.ModifyParameter(this);
			}
		}

		public Parameter(FieldInfo field)
			: this(field.FieldType, field.Name, field)
		{
		}

		public Parameter(PropertyInfo property)
			: this(property.PropertyType, property.Name, property)
		{
		}

		public Parameter(ParameterInfo parameter)
			: this(parameter.ParameterType, parameter.Name, parameter)
		{
			DefaultValue = GetDefaultValue(parameter.DefaultValue);

			if (this.Get<ParamArrayAttribute>().Any())
			{
				this.MarkAsRemainder();
			}
		}

		public IImmutableParameter ToImmutable()
			=> new ImmutableParameter(this);

		private static object? GetDefaultValue(object value)
		{
			// Not optional and has no default value
			if (value == DBNull.Value)
			{
				return NoDefaultValue;
			}
			// Optional but has no default value
			if (value == Type.Missing)
			{
				return NoDefaultValue;
			}
			return value;
		}

		[DebuggerDisplay(CommandServiceUtils.DEBUGGER_DISPLAY)]
		private sealed class ImmutableParameter : IImmutableParameter
		{
			public IReadOnlyList<object> Attributes { get; }
			public object? DefaultValue { get; }
			public bool HasDefaultValue { get; }
			public int? Length { get; } = 1;
			public string OriginalParameterName { get; }
			public string ParameterName { get; }
			public Type ParameterType { get; }
			public IReadOnlyDictionary<string, IReadOnlyList<IParameterPrecondition>> Preconditions { get; }
			public string PrimaryId { get; }
			public ITypeReader? TypeReader { get; }
			IEnumerable<object> IQueryableEntity.Attributes => Attributes;
			private string DebuggerDisplay => this.FormatForDebuggerDisplay();

			public ImmutableParameter(Parameter mutable)
			{
				DefaultValue = mutable.DefaultValue;
				HasDefaultValue = mutable.HasDefaultValue;
				OriginalParameterName = mutable.OriginalParameterName;
				ParameterType = mutable.ParameterType;

				var attributes = ImmutableArray.CreateBuilder<object>(mutable.Attributes.Count);
				// Use ConcurrentDictionary because it has GetOrAdd by default, not threading reasons
				var preconditions = new ConcurrentDictionary<string, List<IParameterPrecondition>>();
				int l = 0, n = 0, t = 0;
				foreach (var attribute in mutable.Attributes)
				{
					attributes.Add(attribute);
					switch (attribute)
					{
						case IParameterPrecondition precondition:
							if (precondition.Groups.Count == 0)
							{
								preconditions
									.GetOrAdd(string.Empty, _ => new())
									.Add(precondition);
							}
							else
							{
								foreach (var group in precondition.Groups)
								{
									preconditions
										.GetOrAdd(group, _ => new())
										.Add(precondition);
								}
							}
							break;

						case ILengthAttribute length:
							Length = length.ThrowIfDuplicate(x => x.Length, ref l);
							break;

						case INameAttribute name:
							ParameterName = name.ThrowIfDuplicate(x => x.Name, ref n);
							break;

						case IOverrideTypeReaderAttribute typeReader:
							typeReader.Reader.ThrowIfInvalidTypeReader(ParameterType);
							TypeReader = typeReader.ThrowIfDuplicate(x => x.Reader, ref t);
							break;

						case IIdAttribute id:
							PrimaryId ??= id.Id;
							break;
					}
				}
				Attributes = attributes.MoveToImmutable();
				Preconditions = preconditions.ToImmutableDictionary(
					x => x.Key,
					x => (IReadOnlyList<IParameterPrecondition>)x.Value.ToImmutableArray()
				);

				TypeReader ??= mutable.TypeReader;
				ParameterName ??= mutable.OriginalParameterName;
				PrimaryId ??= Guid.NewGuid().ToString();
			}
		}
	}
}