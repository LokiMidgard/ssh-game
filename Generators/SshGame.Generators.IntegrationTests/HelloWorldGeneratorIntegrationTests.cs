using Microsoft.VisualStudio.TestTools.UnitTesting;
using Roslyn.Generated;

namespace SshGame.Generators.IntegrationTests
{
	[TestClass]
	public class HelloWorldGeneratorIntegrationTests
	{
		[TestMethod]
		public void Generated_HelloWorld()
		{
			string greeting = Greeter.GetHelloWorld();

			Assert.AreEqual("Hello, World!", greeting);
		}
	}

	internal static partial class Greeter
	{
		[HelloWorld]
		public static partial string GetHelloWorld();
	}
}
