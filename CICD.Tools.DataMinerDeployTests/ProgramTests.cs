﻿namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Skyline.DataMiner.CICD.Tools.DataMinerDeploy;

    [TestClass]
    public class ProgramTests
    {
        [TestMethod]
        public void ExtractOldArtifactIdTest_withDebug_fromPowershell()
        {
            string input = "[11:43:50 DBG] Attempting upload with provided argument as token for artifact: C:\\CreatedPackages\\HelloFromCommandLine.dmapp... [11:43:50 DBG] Uploading C:\\CreatedPackages\\HelloFromCommandLine.dmapp... [11:43:50 DBG] HTTP Post with info:--name HelloFromCommandLine --version 0.0.0-CU0 --contentType DmScript --branch main --identifier  --isPrerelease System.Func`1[System.Boolean] --developer  --releasepath  --file HelloFromCommandLine [11:43:51 DBG] The upload api returned a OK response. Body: System.Net.Http.HttpConnectionResponseContent [11:43:51 DBG] Finished Uploading C:\\CreatedPackages\\HelloFromCommandLine.dmapp [11:43:51 INF] {artifactId:dmscript/000-0-0000-000}";
            var result = Program.ExtractArtifactId(input);

            Assert.AreEqual("dmscript/000-0-0000-000", result);
        }

        [TestMethod]
        public void ExtractOldArtifactIdTest_withDebugHalfway_fromPowershell()
        {
            string input = "[11:43:50 DBG] Attempting upload with provided argument as token for artifact: C:\\CreatedPackages\\HelloFromCommandLine.dmapp... [11:43:50 DBG] Uploading C:\\CreatedPackages\\HelloFromCommandLine.dmapp...[11:43:51 INF] {artifactId:dmscript/000-0-0000-000} [11:43:50 DBG] HTTP Post with info:--name HelloFromCommandLine --version 0.0.0-CU0 --contentType DmScript --branch main --identifier  --isPrerelease System.Func`1[System.Boolean] --developer  --releasepath  --file HelloFromCommandLine [11:43:51 DBG] The upload api returned a OK response. Body: System.Net.Http.HttpConnectionResponseContent [11:43:51 DBG] Finished Uploading C:\\CreatedPackages\\HelloFromCommandLine.dmapp";
            var result = Program.ExtractArtifactId(input);

            Assert.AreEqual("dmscript/000-0-0000-000", result);
        }

        [TestMethod]
        public void ExtractOldArtifactIdTest_withDebug_Direct()
        {
            string input = "[11:43:50 DBG] Attempting upload with provided argument as token for artifact: C:\\CreatedPackages\\HelloFromCommandLine.dmapp... [11:43:50 DBG] Uploading C:\\CreatedPackages\\HelloFromCommandLine.dmapp... [11:43:50 DBG] HTTP Post with info:--name HelloFromCommandLine --version 0.0.0-CU0 --contentType DmScript --branch main --identifier  --isPrerelease System.Func`1[System.Boolean] --developer  --releasepath  --file HelloFromCommandLine [11:43:51 DBG] The upload api returned a OK response. Body: System.Net.Http.HttpConnectionResponseContent [11:43:51 DBG] Finished Uploading C:\\CreatedPackages\\HelloFromCommandLine.dmapp [11:43:51 INF] {\"artifactId\":\"dmscript/000-0-0000-000\"}";
            var result = Program.ExtractArtifactId(input);

            Assert.AreEqual("dmscript/000-0-0000-000", result);
        }

        [TestMethod]
        public void ExtractOldArtifactIdTest_Direct()
        {
            string input = "dmscript/000-0-0000-000";
            var result = Program.ExtractArtifactId(input);

            Assert.AreEqual("dmscript/000-0-0000-000", result);
        }

        [TestMethod]
        public void ExtractOldArtifactIdTest_fromOutput()
        {
            string input = "[11:43:51 INF] {\"artifactId\":\"dmscript/000-0-0000-000\"}";
            var result = Program.ExtractArtifactId(input);

            Assert.AreEqual("dmscript/000-0-0000-000", result);
        }

        [TestMethod]
        public void ExtractOldArtifactIdTest_fromOutput_powershell()
        {
            string input = "[11:43:51 INF] {artifactId:dmscript/000-0-0000-000}";
            var result = Program.ExtractArtifactId(input);

            Assert.AreEqual("dmscript/000-0-0000-000", result);
        }

        [TestMethod]
        public void ExtractOldArtifactIdTest_fromOutput_DoubleQuoteEscaping()
        {
            string input = "[11:43:51 INF] {\"\"artifactId\"\":\"\"dmscript/000-0-0000-000\"\"}";
            var result = Program.ExtractArtifactId(input);

            Assert.AreEqual("dmscript/000-0-0000-000", result);
        }

        [TestMethod]
        public void ExtractOldArtifactIdTest_fromOutput_SingleQuoteEscaping()
        {
            string input = "[11:43:51 INF] {\'\"artifactId\'\":\'\"dmscript/000-0-0000-000\'\"}";
            var result = Program.ExtractArtifactId(input);

            Assert.AreEqual("dmscript/000-0-0000-000", result);
        }

        [TestMethod]
        public void ExtractOldArtifactIdTest_fromOutput_BackslashEscaping()
        {
            string input = "[11:43:51 INF] {\\\"artifactId\\\":\\\"dmscript/000-0-0000-000\\\"}";
            var result = Program.ExtractArtifactId(input);

            Assert.AreEqual("dmscript/000-0-0000-000", result);
        }

        [TestMethod]
        public void ExtractOldArtifactIdTest_Direct_Letters()
        {
            string input = "dmscript/abc-0-0ABC-00A";
            var result = Program.ExtractArtifactId(input);

            Assert.AreEqual("dmscript/abc-0-0ABC-00A", result);
        }
    }
}