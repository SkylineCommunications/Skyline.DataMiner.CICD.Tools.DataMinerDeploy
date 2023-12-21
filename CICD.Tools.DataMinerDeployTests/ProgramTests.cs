using Microsoft.VisualStudio.TestTools.UnitTesting;

using Skyline.DataMiner.CICD.Tools.DataMinerDeploy;

namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Tests
{
	[TestClass()]
	public class ProgramTests
	{
		[TestMethod()]
		public void ExtractArtifactIdTest_withDebug_fromPowershell()
		{
			string input = "[11:43:50 DBG] Attempting upload with provided argument as token for artifact: C:\\CreatedPackages\\HelloFromCommandLine.dmapp... [11:43:50 DBG] Uploading C:\\CreatedPackages\\HelloFromCommandLine.dmapp... [11:43:50 DBG] HTTP Post with info:--name HelloFromCommandLine --version 0.0.0-CU0 --contentType DmScript --branch main --identifier  --isPrerelease System.Func`1[System.Boolean] --developer  --releasepath  --file HelloFromCommandLine [11:43:51 DBG] The upload api returned a OK response. Body: System.Net.Http.HttpConnectionResponseContent [11:43:51 DBG] Finished Uploading C:\\CreatedPackages\\HelloFromCommandLine.dmapp [11:43:51 INF] {artifactId:dmscript/000-0-0000-000}";
			var result = Program.ExtractArtifactId(input);

			Assert.AreEqual("dmscript/000-0-0000-000", result);
		}
		[TestMethod()]
		public void ExtractArtifactIdTest_withDebug_Direct()
		{
			string input = "[11:43:50 DBG] Attempting upload with provided argument as token for artifact: C:\\CreatedPackages\\HelloFromCommandLine.dmapp... [11:43:50 DBG] Uploading C:\\CreatedPackages\\HelloFromCommandLine.dmapp... [11:43:50 DBG] HTTP Post with info:--name HelloFromCommandLine --version 0.0.0-CU0 --contentType DmScript --branch main --identifier  --isPrerelease System.Func`1[System.Boolean] --developer  --releasepath  --file HelloFromCommandLine [11:43:51 DBG] The upload api returned a OK response. Body: System.Net.Http.HttpConnectionResponseContent [11:43:51 DBG] Finished Uploading C:\\CreatedPackages\\HelloFromCommandLine.dmapp [11:43:51 INF] {\"artifactId\":\"dmscript/000-0-0000-000\"}";
			var result = Program.ExtractArtifactId(input);

			Assert.AreEqual("dmscript/000-0-0000-000", result);
		}
		[TestMethod()]
		public void ExtractArtifactIdTest_Direct()
		{
			string input = "dmscript/000-0-0000-000";
			var result = Program.ExtractArtifactId(input);

			Assert.AreEqual("dmscript/000-0-0000-000", result);
		}
		[TestMethod()]
		public void ExtractArtifactIdTest_fromOutput()
		{
			string input = "[11:43:51 INF] {\"artifactId\":\"dmscript/000-0-0000-000\"}";
			var result = Program.ExtractArtifactId(input);

			Assert.AreEqual("dmscript/000-0-0000-000", result);
		}

		[TestMethod()]
		public void ExtractArtifactIdTest_fromOutput_powershell()
		{
			string input = "[11:43:51 INF] {artifactId:dmscript/000-0-0000-000}";
			var result = Program.ExtractArtifactId(input);

			Assert.AreEqual("dmscript/000-0-0000-000", result);
		}

		[TestMethod()]
		public void ExtractArtifactIdTest_fromOutput_DoubleQuoteEscaping()
		{
			string input = "[11:43:51 INF] {\"\"artifactId\"\":\"\"dmscript/000-0-0000-000\"\"}";
			var result = Program.ExtractArtifactId(input);

			Assert.AreEqual("dmscript/000-0-0000-000", result);
		}

		[TestMethod()]
		public void ExtractArtifactIdTest_fromOutput_SingleQuoteEscaping()
		{
			string input = "[11:43:51 INF] {\'\"artifactId\'\":\'\"dmscript/000-0-0000-000\'\"}";
			var result = Program.ExtractArtifactId(input);

			Assert.AreEqual("dmscript/000-0-0000-000", result);
		}
		[TestMethod()]
		public void ExtractArtifactIdTest_fromOutput_BackslashEscaping()
		{
			string input = "[11:43:51 INF] {\\\"artifactId\\\":\\\"dmscript/000-0-0000-000\\\"}";
			var result = Program.ExtractArtifactId(input);

			Assert.AreEqual("dmscript/000-0-0000-000", result);
		}
	}
}