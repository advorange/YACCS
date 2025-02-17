﻿using System;

namespace YACCS.Commands.Attributes;

/// <inheritdoc cref="INameAttribute"/>
/// <summary>
/// Creates a new <see cref="NameAttribute"/>.
/// </summary>
/// <param name="name">
/// <inheritdoc cref="Name" path="/summary"/>
/// </param>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
public class NameAttribute(string name)
	: Attribute, INameAttribute
{
	/// <inheritdoc />
	public virtual string Name { get; } = name;
}