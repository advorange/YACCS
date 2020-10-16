﻿using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using YACCS.Commands;
using YACCS.TypeReaders;

namespace YACCS.Tests.TypeReaders
{
	public abstract class TypeReader_Tests<T>
	{
		public virtual IContext Context { get; } = new FakeContext();
		public virtual string Invalid { get; } = "asdf";
		public abstract TypeReader<T> Reader { get; }

		[TestMethod]
		public async Task Invalid_Test()
		{
			var result = await Reader.ReadAsync(Context, Invalid).ConfigureAwait(false);
			Assert.IsFalse(result.InnerResult.IsSuccess);
		}
	}
}