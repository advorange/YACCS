﻿using System;

namespace YACCS.Commands
{
	public sealed class EmptyServiceProvider : IServiceProvider
	{
		public static EmptyServiceProvider Instance { get; } = new EmptyServiceProvider();

		private EmptyServiceProvider()
		{
		}

		public object? GetService(Type serviceType) => null;
	}
}