﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

using YACCS.Parsing;

namespace YACCS.Tests.Parsing
{
	[TestClass]
	public class ParseArgs_Tests
	{
		//A "B "C \"D E\"" F G"
		public const string INPUT_1 = "A \"B \"C \\\"D E\\\"\" F G\"";
		//H "A "B "C \"D E\"" F G"" I J
		public const string INPUT_2 = "H \"" + INPUT_1 + "\" I J";
		//K "L M" N "H "A "B "C \"D E\"" F G"" I J" O "H "A "B "C \"D E\"" F G"" I J" P Q
		public const string INPUT_3 = "K \"L M\" N \"" + INPUT_2 + "\" O \"" + INPUT_2 + "\" P Q";

		[TestMethod]
		public void NestedQuotes1_Test()
		{
			const string INPUT = INPUT_1;
			var parsed = ParseArgs.Parse(INPUT).Arguments;

			Assert.AreEqual(2, parsed.Count);
			Assert.AreEqual("A", parsed[0]);
			Assert.AreEqual("B \"C \\\"D E\\\"\" F G", parsed[1]);
		}

		[TestMethod]
		public void NestedQuotes2_Test()
		{
			const string INPUT = INPUT_2;
			var parsed = ParseArgs.Parse(INPUT).Arguments;

			Assert.AreEqual(4, parsed.Count);
			Assert.AreEqual("H", parsed[0]);
			Assert.AreEqual("A \"B \"C \\\"D E\\\"\" F G\"", parsed[1]);
			Assert.AreEqual("I", parsed[2]);
			Assert.AreEqual("J", parsed[3]);
		}

		[TestMethod]
		public void NestedQuotes3_Test()
		{
			const string INPUT = INPUT_3;
			var parsed = ParseArgs.Parse(INPUT).Arguments;

			Assert.AreEqual(8, parsed.Count);
			Assert.AreEqual("K", parsed[0]);
			Assert.AreEqual("L M", parsed[1]);
			Assert.AreEqual("N", parsed[2]);
			Assert.AreEqual("H \"A \"B \"C \\\"D E\\\"\" F G\"\" I J", parsed[3]);
			Assert.AreEqual("O", parsed[4]);
			Assert.AreEqual("H \"A \"B \"C \\\"D E\\\"\" F G\"\" I J", parsed[5]);
			Assert.AreEqual("P", parsed[6]);
			Assert.AreEqual("Q", parsed[7]);
		}

		[TestMethod]
		public void SimpleQuotes_Test()
		{
			const string INPUT = "\"test value \"aaaaaa\" dog\"";
			var parsed = ParseArgs.Parse(INPUT).Arguments;

			var expected = INPUT[1..^1];
			Assert.AreEqual(1, parsed.Count);
			Assert.AreEqual(expected, parsed[0]);
		}
	}
}